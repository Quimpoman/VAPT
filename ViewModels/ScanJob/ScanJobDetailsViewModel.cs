namespace EVOLVE_VAPT.ViewModels.ScanJob
{
    public class ScanJobDetailsViewModel
    {
        public long ScanJobID { get; set; }

        public string JobCode { get; set; } = string.Empty;


        public int AssessmentTargetID { get; set; }

        public string AssessmentCode { get; set; } = string.Empty;

        public string AssessmentName { get; set; } = string.Empty;

        public string TargetName { get; set; } = string.Empty;

        public string TargetTypeCode { get; set; } = string.Empty;

        public string TargetValue { get; set; } = string.Empty;


        public int ScanProfileID { get; set; }

        public string ScanProfileCode { get; set; } = string.Empty;

        public string ScanProfileName { get; set; } = string.Empty;

        public string ScannerTypeCode { get; set; } = string.Empty;


        public string ScanJobStatusCode { get; set; } = string.Empty;

        public string ScanJobStatusName { get; set; } = string.Empty;

        public bool IsTerminal { get; set; }

        public bool RequiresExplicitApproval { get; set; }


        public string? RequestedByEmail { get; set; }

        public DateTime RequestedDate { get; set; }


        public string? ApprovedByEmail { get; set; }

        public DateTime? ApprovedDate { get; set; }


        public string? RejectedByEmail { get; set; }

        public DateTime? RejectedDate { get; set; }


        public string? CancelledByEmail { get; set; }

        public DateTime? CancelledDate { get; set; }

        public string? CancellationReason { get; set; }


        public string? ApprovalNotes { get; set; }

        public DateTime? QueuedDate { get; set; }

        public DateTime? StartedDate { get; set; }

        public DateTime? CompletedDate { get; set; }

        public string? FailureReason { get; set; }


        // Immutable configuration captured at job creation.
        public List<ScanJobScopeSnapshotViewModel> ScopeRules { get; set; }
            = new();

        public List<ScanJobOptionSnapshotViewModel> ProfileOptions { get; set; }
            = new();
    }
}