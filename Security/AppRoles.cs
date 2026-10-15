namespace EVOLVE_VAPT.Security
{
    public static class AppRoles
    {
        public const string Administrator =
            "Administrator";

        public const string SecurityAnalyst =
            "Security Analyst";

        public const string Reviewer =
            "Reviewer";

        public const string ReadOnlyClient =
            "Read-Only / Client";


        public static IReadOnlyList<string> All { get; } =
            new[]
            {
                Administrator,
                SecurityAnalyst,
                Reviewer,
                ReadOnlyClient
            };
    }
}