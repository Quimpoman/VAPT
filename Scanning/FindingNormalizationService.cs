using EVOLVE_VAPT.Models;
using EVOLVE_VAPT.Scanning.Normalization;
using Microsoft.EntityFrameworkCore;

namespace EVOLVE_VAPT.Services
{
    /// <summary>
    /// Converts raw scanner output into common
    /// EVOLVE_VAPT Finding records.
    /// </summary>
    public class FindingNormalizationService
    {
        private readonly EVOLVE_VAPTContext _context;

        private readonly ScanResultNormalizerResolver
            _normalizerResolver;


        public FindingNormalizationService(
            EVOLVE_VAPTContext context,
            ScanResultNormalizerResolver normalizerResolver)
        {
            _context = context;
            _normalizerResolver = normalizerResolver;
        }


        public async Task<(
            bool Success,
            string? ErrorMessage,
            int FindingCount)>
            NormalizeScanResultAsync(
                long scanJobId,
                CancellationToken cancellationToken)
        {
            var scanResult =
                await _context.trn_ScanResult
                    .FirstOrDefaultAsync(
                        x =>
                            x.ScanJobID ==
                                scanJobId,
                        cancellationToken);


            if (scanResult == null)
            {
                return (
                    false,
                    "Raw Scan Result could not be found.",
                    0
                );
            }


            if (!scanResult.IsSuccessful)
            {
                return (
                    false,
                    "Failed Scan Results are not normalized.",
                    0
                );
            }


            if (scanResult.IsNormalized)
            {
                var existingCount =
                    await _context.trn_Finding
                        .CountAsync(
                            x =>
                                x.ScanResultID ==
                                    scanResult.ScanResultID &&
                                x.IsActive == true,
                            cancellationToken);


                return (
                    true,
                    null,
                    existingCount
                );
            }


            var scanJob =
                await _context.trn_ScanJob
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.ScanJobID ==
                                scanJobId,
                        cancellationToken);


            if (scanJob == null)
            {
                return await FailAsync(
                    scanResult,
                    "Scan Job could not be found.",
                    cancellationToken);
            }


            var normalizer =
                _normalizerResolver.Resolve(
                    scanResult.ScannerTypeCode);


            if (normalizer == null)
            {
                return await FailAsync(
                    scanResult,
                    $"No result normalizer is registered for '{scanResult.ScannerTypeCode}'.",
                    cancellationToken);
            }


            IReadOnlyList<NormalizedFinding>
                normalizedFindings;


            try
            {
                normalizedFindings =
                    normalizer.Normalize(
                        scanResult.RawOutput);
            }
            catch (Exception ex)
            {
                return await FailAsync(
                    scanResult,
                    ex.Message,
                    cancellationToken);
            }


            /*
             * A scanner should not return the same source
             * identifier twice within one Scan Result.
             */
            var duplicateSourceId =
                normalizedFindings
                    .GroupBy(
                        x => x.SourceFindingIdentifier,
                        StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault(
                        x => x.Count() > 1);


            if (duplicateSourceId != null)
            {
                return await FailAsync(
                    scanResult,
                    $"Duplicate source finding identifier '{duplicateSourceId.Key}' was returned.",
                    cancellationToken);
            }


            var severityRecords =
                await _context.ref_Severity
                    .Where(x =>
                        x.IsActive == true)
                    .ToListAsync(
                        cancellationToken);


            var severityLookup =
                severityRecords.ToDictionary(
                    x => x.SeverityCode,
                    StringComparer.OrdinalIgnoreCase);


            /*
             * Fail the entire normalization operation when
             * the scanner returns an unknown severity.
             *
             * We do not silently guess severity.
             */
            foreach (var finding in normalizedFindings)
            {
                if (!severityLookup.ContainsKey(
                    finding.SeverityCode))
                {
                    return await FailAsync(
                        scanResult,
                        $"Unknown severity '{finding.SeverityCode}' returned for finding '{finding.SourceFindingIdentifier}'.",
                        cancellationToken);
                }
            }


            var newStatus =
                await _context.ref_FindingStatus
                    .FirstOrDefaultAsync(
                        x =>
                            x.FindingStatusCode ==
                                "NEW" &&
                            x.IsActive == true,
                        cancellationToken);


            if (newStatus == null)
            {
                return await FailAsync(
                    scanResult,
                    "The New Finding status is not configured.",
                    cancellationToken);
            }


            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync(
                        cancellationToken);


            try
            {
                var existingIdentifiers =
                    await _context.trn_Finding
                        .Where(x =>
                            x.ScanResultID ==
                                scanResult.ScanResultID)
                        .Select(x =>
                            x.SourceFindingIdentifier)
                        .ToListAsync(
                            cancellationToken);


                var existingSet =
                    existingIdentifiers.ToHashSet(
                        StringComparer.OrdinalIgnoreCase);


                foreach (var finding in normalizedFindings)
                {
                    // Makes repeated normalization safe.
                    if (existingSet.Contains(
                        finding.SourceFindingIdentifier))
                    {
                        continue;
                    }


                    var severity =
                        severityLookup[
                            finding.SeverityCode];


                    _context.trn_Finding.Add(
                        new trn_Finding
                        {
                            ScanResultID =
                                scanResult.ScanResultID,

                            ScanJobID =
                                scanJob.ScanJobID,

                            AssessmentTargetID =
                                scanJob.AssessmentTargetID,

                            SourceFindingIdentifier =
                                finding.SourceFindingIdentifier,

                            Title =
                                finding.Title,

                            SeverityID =
                                severity.SeverityID,

                            FindingStatusID =
                                newStatus.FindingStatusID,

                            TargetValue =
                                string.IsNullOrWhiteSpace(
                                    finding.TargetValue)
                                    ? scanJob.TargetValueSnapshot
                                    : finding.TargetValue,

                            Description =
                                finding.Description,

                            Evidence =
                                finding.Evidence,

                            Recommendation =
                                finding.Recommendation,

                            Reference =
                                finding.Reference,

                            ScannerTypeCode =
                                scanResult.ScannerTypeCode,

                            ScannerVersion =
                                scanResult.ScannerVersion,

                            IsActive =
                                true,

                            CreatedDate =
                                DateTime.Now
                        });
                }


                scanResult.IsNormalized =
                    true;

                scanResult.NormalizedDate =
                    DateTime.Now;

                scanResult.NormalizationError =
                    null;


                await _context.SaveChangesAsync(
                    cancellationToken);


                await transaction.CommitAsync(
                    cancellationToken);


                return (
                    true,
                    null,
                    normalizedFindings.Count
                );
            }
            catch
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                throw;
            }
        }


        private async Task<(
            bool Success,
            string? ErrorMessage,
            int FindingCount)>
            FailAsync(
                trn_ScanResult scanResult,
                string message,
                CancellationToken cancellationToken)
        {
            scanResult.IsNormalized =
                false;

            scanResult.NormalizedDate =
                null;

            scanResult.NormalizationError =
                Limit(message, 2000);


            await _context.SaveChangesAsync(
                cancellationToken);


            return (
                false,
                scanResult.NormalizationError,
                0
            );
        }


        private static string? Limit(
            string? value,
            int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }


            return value.Length <= maximumLength
                ? value
                : value[..maximumLength];
        }
    }
}