namespace EVOLVE_VAPT.ViewModels.AuditTrail
{
    public class AuditTrailIndexViewModel
    {
        // Filters
        public string? Search { get; set; }

        public string? UserName { get; set; }

        public string? Module { get; set; }

        public string? ActionType { get; set; }

        public bool? IsSuccessful { get; set; }

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }


        // Filter options
        public IReadOnlyList<string> Users { get; set; } =
            Array.Empty<string>();

        public IReadOnlyList<string> Modules { get; set; } =
            Array.Empty<string>();

        public IReadOnlyList<string> ActionTypes { get; set; } =
            Array.Empty<string>();


        // Results
        public IReadOnlyList<AuditTrailListItemViewModel> Items
        {
            get;
            set;
        } =
            Array.Empty<AuditTrailListItemViewModel>();


        // Pagination
        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 50;

        public int TotalCount { get; set; }


        public int TotalPages =>
            PageSize <= 0
                ? 0
                : (int)Math.Ceiling(
                    TotalCount / (double)PageSize);
    }
}