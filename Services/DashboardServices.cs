using EVOLVE_VAPT.Models;
using EVOLVE_VAPT.ViewModels.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace EVOLVE_VAPT.Services
{
    public class DashboardService
    {
        private readonly EVOLVE_VAPTContext _context;


        public DashboardService(
            EVOLVE_VAPTContext context)
        {
            _context = context;
        }


        public async Task<DashboardViewModel>
            GetDashboardAsync()
        {
            var model =
                new DashboardViewModel();


            // -------------------------------------------------
            // Top KPI cards
            // -------------------------------------------------

            model.ActiveAssessments =
                await _context
                    .vwGet_AssessmentData
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.IsActive == true &&
                        x.AssessmentStatusCode !=
                            "COMPLETED" &&
                        x.AssessmentStatusCode !=
                            "CANCELLED");


            model.RunningScans =
                await _context
                    .vwGet_ScanJobData
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.ScanJobStatusCode ==
                            "RUNNING");


            model.OpenFindings =
                await _context
                    .vwGet_FindingData
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.IsActive == true &&
                        x.FindingStatusCode !=
                            "CLOSED" &&
                        x.FindingStatusCode !=
                            "FALSE_POSITIVE");


            model.PendingRemediation =
                await _context
                    .vwGet_FindingData
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.IsActive == true &&
                        x.FindingStatusCode ==
                            "REMEDIATION");


            // -------------------------------------------------
            // Severity Distribution
            //
            // Only unresolved findings represent current risk.
            // -------------------------------------------------

            var severityCounts =
                await _context
                    .vwGet_FindingData
                    .AsNoTracking()

                    .Where(x =>
                        x.IsActive == true &&
                        x.FindingStatusCode !=
                            "CLOSED" &&
                        x.FindingStatusCode !=
                            "FALSE_POSITIVE")

                    .GroupBy(x =>
                        x.SeverityCode)

                    .Select(x => new
                    {
                        SeverityCode =
                            x.Key,

                        Count =
                            x.Count()
                    })

                    .ToDictionaryAsync(
                        x => x.SeverityCode,
                        x => x.Count);


            /*
             * Start from ref_Severity so even severities with
             * zero findings still appear on the dashboard.
             */
            var severities =
                await _context.ref_Severity
                    .AsNoTracking()

                    .Where(x =>
                        x.IsActive == true)

                    .OrderByDescending(x =>
                        x.SortOrder)

                    .ToListAsync();


            model.SeverityDistribution =
                severities
                    .Select(x =>
                        new DashboardSeverityViewModel
                        {
                            SeverityCode =
                                x.SeverityCode,

                            SeverityName =
                                x.SeverityName,

                            Count =
                                severityCounts
                                    .GetValueOrDefault(
                                        x.SeverityCode,
                                        0)
                        })

                    .ToList();


            // -------------------------------------------------
            // Remediation workflow summary
            // -------------------------------------------------

            var findingStatusCounts =
                await _context
                    .vwGet_FindingData
                    .AsNoTracking()

                    .Where(x =>
                        x.IsActive == true)

                    .GroupBy(x =>
                        x.FindingStatusCode)

                    .Select(x => new
                    {
                        FindingStatusCode =
                            x.Key,

                        Count =
                            x.Count()
                    })

                    .ToDictionaryAsync(
                        x => x.FindingStatusCode,
                        x => x.Count);


            model.RemediationStatus =
                new List<DashboardRemediationViewModel>
                {
                    new()
                    {
                        Label =
                            "For Remediation",

                        FindingStatusCode =
                            "CONFIRMED",

                        Count =
                            findingStatusCounts
                                .GetValueOrDefault(
                                    "CONFIRMED",
                                    0)
                    },

                    new()
                    {
                        Label =
                            "In Progress",

                        FindingStatusCode =
                            "REMEDIATION",

                        Count =
                            findingStatusCounts
                                .GetValueOrDefault(
                                    "REMEDIATION",
                                    0)
                    },

                    new()
                    {
                        Label =
                            "Retest Required",

                        FindingStatusCode =
                            "RETEST",

                        Count =
                            findingStatusCounts
                                .GetValueOrDefault(
                                    "RETEST",
                                    0)
                    },

                    new()
                    {
                        Label =
                            "Resolved",

                        FindingStatusCode =
                            "CLOSED",

                        Count =
                            findingStatusCounts
                                .GetValueOrDefault(
                                    "CLOSED",
                                    0)
                    }
                };


            // -------------------------------------------------
            // Recent Findings
            // -------------------------------------------------

            model.RecentFindings =
                await _context
                    .vwGet_FindingData
                    .AsNoTracking()

                    .Where(x =>
                        x.IsActive == true)

                    .OrderByDescending(x =>
                        x.CreatedDate)

                    .Take(5)

                    .Select(x =>
                        new DashboardRecentFindingViewModel
                        {
                            FindingID =
                                x.FindingID,

                            Title =
                                x.Title,

                            SeverityCode =
                                x.SeverityCode,

                            SeverityName =
                                x.SeverityName,

                            FindingStatusName =
                                x.FindingStatusName,

                            TargetValue =
                                x.TargetValue,

                            AssessmentCode =
                                x.AssessmentCode,

                            CreatedDate =
                                x.CreatedDate
                        })

                    .ToListAsync();


            // -------------------------------------------------
            // Recent Scan Jobs
            // -------------------------------------------------

            model.RecentScanJobs =
                await _context
                    .vwGet_ScanJobData
                    .AsNoTracking()

                    .OrderByDescending(x =>
                        x.RequestedDate)

                    .Take(5)

                    .Select(x =>
                        new DashboardRecentScanJobViewModel
                        {
                            ScanJobID =
                                x.ScanJobID,

                            JobCode =
                                x.JobCode,

                            ScanJobStatusCode =
                                x.ScanJobStatusCode,

                            ScanJobStatusName =
                                x.ScanJobStatusName,

                            ScanJobPurposeCode =
                                x.ScanJobPurposeCode,

                            ScannerTypeCode =
                                x.ScannerTypeCodeSnapshot,

                            TargetValue =
                                x.TargetValueSnapshot,

                            RequestedDate =
                                x.RequestedDate
                        })

                    .ToListAsync();


            return model;
        }
    }
}