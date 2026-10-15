using System.Diagnostics;
using System.Text;

namespace EVOLVE_VAPT.Scanning.Execution
{
    public sealed class ExternalProcessRunner
        : IExternalProcessRunner
    {
        private readonly ILogger<ExternalProcessRunner>
            _logger;


        public ExternalProcessRunner(
            ILogger<ExternalProcessRunner> logger)
        {
            _logger = logger;
        }

        private static async Task<(string Output, bool Truncated)> ReadWithLimitAsync(
        StreamReader reader,
        int maxCharacters,
        CancellationToken cancellationToken)
        {
            var buffer =
                new char[4096];


            var builder =
                new StringBuilder(
                    Math.Min(
                        maxCharacters,
                        16_384));


            var truncated =
                false;


            try
            {
                while (true)
                {
                    var charactersRead =
                        await reader.ReadAsync(
                            buffer.AsMemory(),
                            cancellationToken);


                    if (charactersRead == 0)
                    {
                        break;
                    }


                    /*
                     * Continue draining after reaching the storage
                     * limit so the child process cannot block on a
                     * full stdout/stderr pipe.
                     */
                    var remaining =
                        maxCharacters -
                        builder.Length;


                    if (remaining > 0)
                    {
                        var charactersToAppend =
                            Math.Min(
                                remaining,
                                charactersRead);


                        builder.Append(
                            buffer,
                            0,
                            charactersToAppend);


                        if (charactersToAppend <
                            charactersRead)
                        {
                            truncated = true;
                        }
                    }
                    else
                    {
                        truncated = true;
                    }
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Return any output collected before cancellation.
            }
            catch (ObjectDisposedException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Stream was closed while terminating the process.
            }
            catch (IOException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Pipe was closed while terminating the process.
            }


            return (
                builder.ToString(),
                truncated
            );
        }
        private static void ValidateRequest(
    ExternalProcessRequest request)
        {
            if (string.IsNullOrWhiteSpace(
                request.ExecutablePath))
            {
                throw new ArgumentException(
                    "Executable path is required.");
            }


            /*
             * Require an explicit executable location.
             * Scanner adapters should not rely on PATH resolution
             * in production.
             */
            if (!Path.IsPathFullyQualified(
                request.ExecutablePath))
            {
                throw new ArgumentException(
                    "Executable path must be fully qualified.");
            }


            if (!File.Exists(
                request.ExecutablePath))
            {
                throw new FileNotFoundException(
                    "Configured executable could not be found.",
                    request.ExecutablePath);
            }


            if (request.Timeout <=
                TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Process timeout must be greater than zero.");
            }


            if (request.MaxStandardOutputCharacters <
                1)
            {
                throw new ArgumentException(
                    "Maximum standard output size must be greater than zero.");
            }


            if (request.MaxStandardErrorCharacters <
                1)
            {
                throw new ArgumentException(
                    "Maximum standard error size must be greater than zero.");
            }


            if (!string.IsNullOrWhiteSpace(
                    request.WorkingDirectory))
            {
                if (!Path.IsPathFullyQualified(
                    request.WorkingDirectory))
                {
                    throw new ArgumentException(
                        "Working directory must be fully qualified.");
                }


                if (!Directory.Exists(
                    request.WorkingDirectory))
                {
                    throw new DirectoryNotFoundException(
                        $"Working directory '{request.WorkingDirectory}' does not exist.");
                }
            }


            foreach (var argument in request.Arguments)
            {
                if (argument.Contains('\0'))
                {
                    throw new ArgumentException(
                        "Process arguments cannot contain null characters.");
                }
            }


            RejectShellExecutable(
                request.ExecutablePath);
        }
        private static void RejectShellExecutable(
    string executablePath)
        {
            var fileName =
                Path.GetFileName(
                    executablePath);


            var prohibitedExecutables =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                "cmd.exe",
                "powershell.exe",
                "pwsh.exe",
                "bash.exe",
                "sh.exe",
                "wscript.exe",
                "cscript.exe",
                "mshta.exe"
                };


            if (prohibitedExecutables.Contains(
                fileName))
            {
                throw new InvalidOperationException(
                    $"Shell or script-host executable '{fileName}' is not permitted for scanner execution.");
            }

            var extension = Path.GetExtension(executablePath);


            var prohibitedExtensions =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                ".bat",
                ".cmd",
                ".ps1",
                ".vbs",
                ".wsf",
                ".js",
                ".sh"
                };


            if (prohibitedExtensions.Contains(extension))
            {
                throw new InvalidOperationException(
                    $"Script file '{extension}' is not permitted for scanner execution.");
            }

        }
        public async Task<ExternalProcessResult> RunAsync(ExternalProcessRequest request, CancellationToken cancellationToken)
        {
            ValidateRequest(request);


            var stopwatch =
                Stopwatch.StartNew();


            var startInfo =
                new ProcessStartInfo
                {
                    FileName =
                        Path.GetFullPath(
                            request.ExecutablePath),

                    UseShellExecute =
                        false,

                    RedirectStandardOutput =
                        true,

                    RedirectStandardError =
                        true,

                    RedirectStandardInput =
                        false,

                    CreateNoWindow =
                        true
                };


            if (!string.IsNullOrWhiteSpace(
                request.WorkingDirectory))
            {
                startInfo.WorkingDirectory =
                    Path.GetFullPath(
                        request.WorkingDirectory);
            }


            /*
             * ArgumentList keeps each argument separate.
             * No manually constructed command line is required.
             */
            foreach (var argument in
                request.Arguments)
            {
                startInfo.ArgumentList.Add(
                    argument);
            }


            using var process =
                new Process
                {
                    StartInfo =
                        startInfo,

                    EnableRaisingEvents =
                        true
                };


            try
            {
                if (!process.Start())
                {
                    stopwatch.Stop();


                    return new ExternalProcessResult
                    {
                        Started = false,

                        StartError =
                            "The operating system did not start the process.",

                        Duration =
                            stopwatch.Elapsed
                    };
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();


                _logger.LogError(
                    ex,
                    "External scanner process failed to start.");


                return new ExternalProcessResult
                {
                    Started = false,

                    StartError =
                        ex.Message,

                    Duration =
                        stopwatch.Elapsed
                };
            }


            _logger.LogInformation(
                "Started external process {ProcessId} using executable {Executable}.",
                process.Id,
                Path.GetFileName(
                    request.ExecutablePath));


            /*
             * Drain stdout and stderr concurrently to prevent
             * either OS pipe from blocking scanner execution.
             */
            using var timeoutSource =
     new CancellationTokenSource(
         request.Timeout);


            using var executionSource =
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        cancellationToken,
                        timeoutSource.Token);


            /*
             * Output readers have their own cancellation source.
             *
             * The scanner timeout controls the process itself.
             * After the process exits, stdout/stderr receive only
             * a short grace period to finish draining.
             */
            using var outputSource =
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        cancellationToken);


            var standardOutputTask =
                ReadWithLimitAsync(
                    process.StandardOutput,
                    request.MaxStandardOutputCharacters,
                    outputSource.Token);


            var standardErrorTask =
                ReadWithLimitAsync(
                    process.StandardError,
                    request.MaxStandardErrorCharacters,
                    outputSource.Token);


            var timedOut =
                false;

            var cancelled =
                false;


            try
            {
                /*
                 * The configured scan timeout applies to the
                 * external scanner process itself.
                 */
                await process.WaitForExitAsync(
                    executionSource.Token);

                _logger.LogInformation(
      "External process {ProcessId} exited. Beginning stdout/stderr drain.",
      process.Id);

            }
            catch (OperationCanceledException)
            {
                timedOut =
                    timeoutSource.IsCancellationRequested &&
                    !cancellationToken.IsCancellationRequested;


                cancelled =
                    cancellationToken.IsCancellationRequested;


                /*
                 * Real scanners may create subprocesses, so terminate
                 * the complete process tree whenever possible.
                 */
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(
                            entireProcessTree: true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to terminate external process {ProcessId}.",
                        process.Id);
                }


                /*
                 * Never wait forever for a killed scanner process.
                 */
                try
                {
                    using var terminationSource =
                        new CancellationTokenSource(
                            TimeSpan.FromSeconds(10));


                    if (!process.HasExited)
                    {
                        await process.WaitForExitAsync(
                            terminationSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning(
                        "Timed out while waiting for external process {ProcessId} to terminate.",
                        process.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Error while waiting for terminated process {ProcessId}.",
                        process.Id);
                }


              
            }

            /*
     * Put the second diagnostic log HERE.
     */
            _logger.LogInformation(
                "Output drain completed for external process {ProcessId}.",
                process.Id);


            (string Output, bool Truncated)
        standardOutput =
            (string.Empty, false);


        (string Output, bool Truncated)
        standardError =
            (string.Empty, false);


            try
            {
                standardOutput =
                    await standardOutputTask
                        .WaitAsync(
                            TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                _logger.LogWarning(
                    "Unable to fully collect stdout from external process {ProcessId}.",
                    process.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error collecting stdout from external process {ProcessId}.",
                    process.Id);
            }


            try
            {
                standardError =
                    await standardErrorTask
                        .WaitAsync(
                            TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                _logger.LogWarning(
                    "Unable to fully collect stderr from external process {ProcessId}.",
                    process.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error collecting stderr from external process {ProcessId}.",
                    process.Id);
            }


            stopwatch.Stop();


            int? exitCode =
                process.HasExited
                    ? process.ExitCode
                    : null;

            _logger.LogInformation(
                "External process {ProcessId} finished. ExitCode={ExitCode}, TimedOut={TimedOut}, Cancelled={Cancelled}, Duration={DurationMs}ms.",
                process.Id,
                exitCode,
                timedOut,
                cancelled,
                stopwatch.ElapsedMilliseconds);


            return new ExternalProcessResult
            {
                Started =
                    true,

                ExitCode =
                    exitCode,

                TimedOut =
                    timedOut,

                Cancelled =
                    cancelled,

                StandardOutput =
                    standardOutput.Output,

                StandardError =
                    standardError.Output,

                StandardOutputTruncated =
                    standardOutput.Truncated,

                StandardErrorTruncated =
                    standardError.Truncated,

                Duration =
                    stopwatch.Elapsed
            };
        }
    }
}