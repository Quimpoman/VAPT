using System.Diagnostics.Contracts;

namespace EVOLVE_VAPT.Scanning.Contracts
{
    /// <summary>
    /// Immutable configuration supplied to a scanner adapter.
    ///
    /// Values come from the Scan Job snapshots rather than
    /// current Target/Profile records.
    /// </summary>
    public class ScannerExecutionContext
    {
        public long ScanJobID { get; init; }

        public string JobCode { get; init; } = string.Empty;


        public string TargetTypeCode { get; init; } = string.Empty;

        public string TargetValue { get; init; } = string.Empty;


        public string ScopeSnapshotJson { get; init; } = "[]";

        public string? ProfileOptionsSnapshotJson { get; init; }


        public bool IsIntrusive { get; init; }

        public int TimeoutMinutes { get; init; }

        public int MaxConcurrency { get; init; }

        public int AssessmentTargetID { get; init; }

        public string ScannerTypeCode { get; init; } = string.Empty;

        public string ScanJobPurposeCode { get; init; } =
    string.Empty;

    }
}

