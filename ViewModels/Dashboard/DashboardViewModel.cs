namespace EVOLVE_VAPT.ViewModels.Dashboard
{
    public class DashboardViewModel
    {
        public int ActiveAssessments { get; set; }

        public int RunningScans { get; set; }

        public int OpenFindings { get; set; }

        public int PendingRemediation { get; set; }


        public List<DashboardSeverityViewModel>
            SeverityDistribution
        { get; set; } = new();


        public List<DashboardRemediationViewModel>
            RemediationStatus
        { get; set; } = new();


        public List<DashboardRecentFindingViewModel>
            RecentFindings
        { get; set; } = new();


        public List<DashboardRecentScanJobViewModel>
            RecentScanJobs
        { get; set; } = new();
    }
}