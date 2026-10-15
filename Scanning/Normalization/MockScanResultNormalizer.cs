using System.Text.Json;

namespace EVOLVE_VAPT.Scanning.Normalization
{
    public class MockScanResultNormalizer :
        IScanResultNormalizer
    {
        public string ScannerTypeCode =>
            "MOCK";


        public IReadOnlyList<NormalizedFinding>
            Normalize(string rawOutput)
        {
            if (string.IsNullOrWhiteSpace(rawOutput))
            {
                throw new InvalidOperationException(
                    "Mock Scanner output is empty.");
            }


            MockRawResult? result;


            try
            {
                result =
                    JsonSerializer.Deserialize<
                        MockRawResult>(
                            rawOutput,
                            new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive =
                                    true
                            });
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    "Mock Scanner output contains invalid JSON.",
                    ex);
            }


            if (result == null)
            {
                throw new InvalidOperationException(
                    "Unable to deserialize Mock Scanner output.");
            }


            if (result.Findings == null ||
                result.Findings.Count == 0)
            {
                return Array.Empty<NormalizedFinding>();
            }


            var findings =
                new List<NormalizedFinding>();


            foreach (var source in result.Findings)
            {
                if (string.IsNullOrWhiteSpace(
                    source.FindingIdentifier))
                {
                    throw new InvalidOperationException(
                        "A Mock finding is missing its source identifier.");
                }


                if (string.IsNullOrWhiteSpace(
                    source.Title))
                {
                    throw new InvalidOperationException(
                        $"Finding '{source.FindingIdentifier}' has no title.");
                }


                if (string.IsNullOrWhiteSpace(
                    source.Severity))
                {
                    throw new InvalidOperationException(
                        $"Finding '{source.FindingIdentifier}' has no severity.");
                }


                findings.Add(
                    new NormalizedFinding
                    {
                        SourceFindingIdentifier =
                            source.FindingIdentifier,

                        Title =
                            source.Title,

                        SeverityCode =
                            source.Severity
                                .Trim()
                                .ToUpperInvariant(),

                        TargetValue =
                            source.Target
                            ?? string.Empty,

                        Description =
                            source.Description,

                        Evidence =
                            source.Evidence,

                        Recommendation =
                            source.Recommendation,

                        Reference =
                            source.Reference
                    });
            }


            return findings;
        }


        private class MockRawResult
        {
            public List<MockRawFinding>? Findings
            {
                get;
                set;
            }
        }


        private class MockRawFinding
        {
            public string FindingIdentifier
            {
                get;
                set;
            } = string.Empty;


            public string Title
            {
                get;
                set;
            } = string.Empty;


            public string Severity
            {
                get;
                set;
            } = string.Empty;


            public string? Target
            {
                get;
                set;
            }


            public string? Description
            {
                get;
                set;
            }


            public string? Evidence
            {
                get;
                set;
            }


            public string? Recommendation
            {
                get;
                set;
            }


            public string? Reference
            {
                get;
                set;
            }
        }
    }
}