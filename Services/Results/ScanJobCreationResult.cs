namespace EVOLVE_VAPT.Services.Results
{
    public class ScanJobCreationResult
    {
        public bool Success { get; init; }

        public string? ErrorMessage { get; init; }

        public long? ScanJobID { get; init; }


        public static ScanJobCreationResult Failed(
            string message)
        {
            return new ScanJobCreationResult
            {
                Success = false,
                ErrorMessage = message
            };
        }


        public static ScanJobCreationResult Created(
            long scanJobId)
        {
            return new ScanJobCreationResult
            {
                Success = true,
                ScanJobID = scanJobId
            };
        }
    }
}