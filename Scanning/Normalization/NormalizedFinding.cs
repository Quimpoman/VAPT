namespace EVOLVE_VAPT.Scanning.Normalization
{
    /// <summary>
    /// Common scanner-independent vulnerability model.
    ///
    /// Every scanner normalizer must eventually produce
    /// this structure.
    /// </summary>
    public class NormalizedFinding
    {
        public string SourceFindingIdentifier { get; init; } =
            string.Empty;

        public string Title { get; init; } =
            string.Empty;

        public string SeverityCode { get; init; } =
            string.Empty;

        public string TargetValue { get; init; } =
            string.Empty;

        public string? Description { get; init; }

        public string? Evidence { get; init; }

        public string? Recommendation { get; init; }

        public string? Reference { get; init; }
    }
}

