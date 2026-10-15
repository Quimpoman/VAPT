namespace EVOLVE_VAPT.ViewModels.AuditTrail
{
    public class AuditTrailListItemViewModel
    {
        public long AuditTrailID { get; set; }

        public DateTime CreatedDate { get; set; }

        public string? UserName { get; set; }

        public string ActionType { get; set; } =
            string.Empty;

        public string Module { get; set; } =
            string.Empty;

        public string? Description { get; set; }

        public string? TableName { get; set; }

        public string? RecordID { get; set; }

        public string? IPAddress { get; set; }

        public bool IsSuccessful { get; set; }
    }
}