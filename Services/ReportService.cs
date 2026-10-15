using EVOLVE_VAPT.Models;
using EVOLVE_VAPT.ViewModels.Report;
using Microsoft.EntityFrameworkCore;

namespace EVOLVE_VAPT.Services
{
    public class ReportService
    {
        private readonly EVOLVE_VAPTContext _context;


        public ReportService(
            EVOLVE_VAPTContext context)
        {
            _context = context;
        }
        public async Task<AssessmentReportViewModel?> GetAssessmentReportAsync(
        int assessmentId)
        {
            var assessment =
                await _context.vwGet_AssessmentData
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.AssessmentID ==
                            assessmentId);


            if (assessment == null)
            {
                return null;
            }


            var model =
                new AssessmentReportViewModel
                {
                    AssessmentID =
                        assessment.AssessmentID,

                    AssessmentCode =
                        assessment.AssessmentCode,

                    AssessmentName =
                        assessment.AssessmentName,

                    Description =
                        assessment.Description,

                    Methodology =
                        assessment.Methodology,

                    Environment =
                        assessment.Environment,

                    AssessmentStatusName =
                        assessment.AssessmentStatusName
                };
            var targets =
    await _context.vwGet_AssessmentTargetData
        .AsNoTracking()

        .Where(x =>
            x.AssessmentID ==
                assessmentId)

        .OrderBy(x =>
            x.TargetName)

        .ToListAsync();


            var assessmentTargetIds =
                targets
                    .Select(x =>
                        x.AssessmentTargetID)
                    .ToList();


            var scopeRules =
                await _context
                    .vwGet_AssessmentScopeRuleData
                    .AsNoTracking()

                    .Where(x =>
                        assessmentTargetIds.Contains(
                            x.AssessmentTargetID) &&
                        x.IsActive == true)

                    .OrderBy(x =>
                        x.ScopeRuleTypeCode)

                    .ThenBy(x =>
                        x.RuleValue)

                    .ToListAsync();


            model.Targets =
                targets
                    .Select(target =>
                        new AssessmentReportTargetViewModel
                        {
                            AssessmentTargetID =
                                target.AssessmentTargetID,

                            TargetName =
                                target.TargetName,

                            TargetTypeCode =
                                target.TargetTypeCode,

                            TargetValue =
                                target.TargetValue,

                            AuthorizationStatusName =
                                target.AuthorizationStatusName,

                            AuthorizationReference =
                                target.AuthorizationReference,

                            AuthorizationValidFrom =
                                target.AuthorizationValidFrom,

                            AuthorizationValidUntil =
                                target.AuthorizationValidUntil,

                            ScopeRules =
                                scopeRules

                                    .Where(rule =>
                                        rule.AssessmentTargetID ==
                                            target.AssessmentTargetID)

                                    .Select(rule =>
                                        new AssessmentReportScopeRuleViewModel
                                        {
                                            ScopeRuleTypeCode =
                                                rule.ScopeRuleTypeCode,

                                            RuleValue =
                                                rule.RuleValue,

                                            Description =
                                                rule.Description
                                        })

                                    .ToList()
                        })

                    .ToList();
            model.ScanJobs =
    await _context.vwGet_ScanJobData
        .AsNoTracking()

        .Where(x =>
            x.AssessmentID ==
                assessmentId)

        .OrderBy(x =>
            x.RequestedDate)

        .Select(x =>
            new AssessmentReportScanJobViewModel
            {
                ScanJobID =
                    x.ScanJobID,

                JobCode =
                    x.JobCode,

                ScanJobPurposeCode =
                    x.ScanJobPurposeCode,

                ScannerTypeCode =
                    x.ScannerTypeCodeSnapshot,

                TargetValue =
                    x.TargetValueSnapshot,

                ScanJobStatusName =
                    x.ScanJobStatusName,

                RequestedDate =
                    x.RequestedDate,

                StartedDate =
                    x.StartedDate,

                CompletedDate =
                    x.CompletedDate
            })

        .ToListAsync();
            var findings =
    await _context.vwGet_FindingData
        .AsNoTracking()

        .Where(x =>
            x.AssessmentID ==
                assessmentId &&
            x.IsActive == true)

        .OrderByDescending(x =>
            x.SeveritySortOrder)

        .ThenBy(x =>
            x.Title)

        .ToListAsync();

            var findingIds =
    findings
        .Select(x =>
            x.FindingID)
        .ToList();

            var remediations =
    await _context
        .vwGet_FindingRemediationData
        .AsNoTracking()

        .Where(x =>
            findingIds.Contains(
                x.FindingID))

        .OrderBy(x =>
            x.CycleNo)

        .ToListAsync();

            var retests =
    await _context
        .vwGet_FindingRetestData
        .AsNoTracking()

        .Where(x =>
            findingIds.Contains(
                x.FindingID))

        .OrderBy(x =>
            x.CycleNo)

        .ToListAsync();

            model.Findings =
    findings
        .Select(finding =>
            new AssessmentReportFindingViewModel
            {
                FindingID =
                    finding.FindingID,

                SourceFindingIdentifier =
                    finding.SourceFindingIdentifier,

                Title =
                    finding.Title,

                SeverityCode =
                    finding.SeverityCode,

                SeverityName =
                    finding.SeverityName,

                FindingStatusCode =
                    finding.FindingStatusCode,

                FindingStatusName =
                    finding.FindingStatusName,

                TargetValue =
                    finding.TargetValue,

                ScannerTypeCode =
                    finding.ScannerTypeCode,

                ScannerVersion =
                    finding.ScannerVersion,

                Description =
                    finding.Description,

                Evidence =
                    finding.Evidence,

                Recommendation =
                    finding.Recommendation,

                Reference =
                    finding.Reference,

                CreatedDate =
                    finding.CreatedDate,


                Remediations =
                    remediations

                        .Where(x =>
                            x.FindingID ==
                                finding.FindingID)

                        .Select(x =>
                            new AssessmentReportRemediationViewModel
                            {
                                FindingRemediationID =
                                    x.FindingRemediationID,

                                CycleNo =
                                    x.CycleNo,

                                RemediationStatusName =
                                    x.RemediationStatusName,

                                AssignedToEmail =
                                    x.AssignedToEmail,

                                TargetDate =
                                    x.TargetDate,

                                RemediationNotes =
                                    x.RemediationNotes,

                                StartedDate =
                                    x.StartedDate,

                                SubmittedForRetestDate =
                                    x.SubmittedForRetestDate,

                                SubmissionNotes =
                                    x.SubmissionNotes
                            })

                        .ToList(),


                Retests =
                    retests

                        .Where(x =>
                            x.FindingID ==
                                finding.FindingID)

                        .Select(x =>
                            new AssessmentReportRetestViewModel
                            {
                                FindingRetestID =
                                    x.FindingRetestID,

                                CycleNo =
                                    x.CycleNo,

                                RetestStatusName =
                                    x.RetestStatusName,

                                RetestOutcomeName =
                                    x.RetestOutcomeName,

                                RetestJobCode =
                                    x.RetestJobCode,

                                ScanJobStatusName =
                                    x.ScanJobStatusName,

                                RequestedDate =
                                    x.RequestedDate,

                                ReviewedDate =
                                    x.ReviewedDate,

                                ReviewedByEmail =
                                    x.ReviewedByEmail,

                                ReviewNotes =
                                    x.ReviewNotes
                            })

                        .ToList()
            })

        .ToList();

            model.TotalFindings =
    model.Findings.Count;


            model.CriticalFindings =
                model.Findings.Count(x =>
                    x.SeverityCode ==
                        "CRITICAL");


            model.HighFindings =
                model.Findings.Count(x =>
                    x.SeverityCode ==
                        "HIGH");


            model.MediumFindings =
                model.Findings.Count(x =>
                    x.SeverityCode ==
                        "MEDIUM");


            model.LowFindings =
                model.Findings.Count(x =>
                    x.SeverityCode ==
                        "LOW");


            model.InformationalFindings =
                model.Findings.Count(x =>
                    x.SeverityCode ==
                        "INFO");


            model.ClosedFindings =
                model.Findings.Count(x =>
                    x.FindingStatusCode ==
                        "CLOSED");


            model.FalsePositiveFindings =
                model.Findings.Count(x =>
                    x.FindingStatusCode ==
                        "FALSE_POSITIVE");


            model.OpenFindings =
                model.Findings.Count(x =>
                    x.FindingStatusCode !=
                        "CLOSED" &&
                    x.FindingStatusCode !=
                        "FALSE_POSITIVE");


            return model;
        }
    }
}