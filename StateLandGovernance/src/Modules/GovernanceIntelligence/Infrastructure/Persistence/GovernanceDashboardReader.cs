using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;

public sealed class GovernanceDashboardReader : IGovernanceDashboardReader
{
    private readonly GovernanceIntelligenceDbContext _dbContext;

    public GovernanceDashboardReader(GovernanceIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<GovernanceDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var complianceList = await _dbContext.ComplianceEvaluations
            .AsNoTracking()
            .OrderByDescending(c => c.EvaluationTimestamp)
            .Take(100)
            .ToListAsync(cancellationToken);

        var conflictList = await _dbContext.ConflictEvaluations
            .Include(c => c.Findings)
            .AsNoTracking()
            .OrderByDescending(c => c.EvaluationTimestamp)
            .Take(100)
            .ToListAsync(cancellationToken);

        var screeningList = await _dbContext.EarlyGovernanceScreeningEvaluations
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        var complaintsList = await _dbContext.ComplaintClassificationAssessments
            .AsNoTracking()
            .OrderByDescending(c => c.AssessedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        // Group into distinct cases
        var candidateCaseIds = complianceList
            .Select(c => c.ProposalId)
            .Where(p => !string.IsNullOrEmpty(p))
            .Concat(screeningList.Select(s => s.CaseId))
            .Concat(complaintsList.Select(c => c.CaseId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var cases = new List<GovernanceCaseAssessmentDto>();
        int clearCount = 0;
        int reviewCount = 0;
        int insufficientCount = 0;

        foreach (var cId in candidateCaseIds)
        {
            if (string.IsNullOrEmpty(cId)) continue;

            var comp = complianceList.FirstOrDefault(c => string.Equals(c.ProposalId, cId, StringComparison.OrdinalIgnoreCase));
            var scrn = screeningList.FirstOrDefault(s => string.Equals(s.CaseId, cId, StringComparison.OrdinalIgnoreCase));
            var cmpl = complaintsList.FirstOrDefault(c => string.Equals(c.CaseId, cId, StringComparison.OrdinalIgnoreCase));
            var conf = conflictList.FirstOrDefault(c => c.Findings.Any(f => string.Equals(f.SubjectId, cId, StringComparison.OrdinalIgnoreCase)));

            string compStatus = comp?.Status ?? "NotAssessed";
            string confStatus = conf is null
                ? "NotAssessed"
                : conf.DetectedConflictsCount > 0 ? "ConflictDetected" : "Clear";

            string earlyStatus = scrn is null ? "NotAssessed" : "Clear";
            if (scrn != null && !string.IsNullOrEmpty(scrn.ResultSnapshotJson))
            {
                if (scrn.ResultSnapshotJson.Contains("ReviewRequired", StringComparison.OrdinalIgnoreCase) ||
                    scrn.ResultSnapshotJson.Contains("RecommendReferral\":true", StringComparison.OrdinalIgnoreCase))
                {
                    earlyStatus = "ReviewRequired";
                }
                else if (scrn.ResultSnapshotJson.Contains("InsufficientInformation", StringComparison.OrdinalIgnoreCase))
                {
                    earlyStatus = "InsufficientInformation";
                }
            }

            bool hasIncompleteAssessment = comp is null || conf is null || scrn is null;
            string overallStatus = (compStatus.Contains("Review", StringComparison.OrdinalIgnoreCase) ||
                                   compStatus.Contains("NonCompliant", StringComparison.OrdinalIgnoreCase) ||
                                   confStatus == "ConflictDetected" ||
                                   earlyStatus == "ReviewRequired")
                ? "ReviewRequired"
                : (hasIncompleteAssessment || compStatus.Contains("Insufficient", StringComparison.OrdinalIgnoreCase) || earlyStatus == "InsufficientInformation")
                    ? "InsufficientInformation"
                    : "Clear";

            if (overallStatus == "Clear") clearCount++;
            else if (overallStatus == "ReviewRequired") reviewCount++;
            else insufficientCount++;

            ComplaintAdvisoryPredictionDto? complaintAdvisory = null;
            if (cmpl != null)
            {
                var probs = new[]
                {
                    (double)cmpl.AdministrativeProceduralIntegrityProbability,
                    (double)cmpl.LeaseRevenuePaymentEnforcementProbability,
                    (double)cmpl.UnauthorizedAllocationTransferUseProbability,
                    (double)cmpl.ProtectedEnvironmentalLeaseMisuseProbability
                };
                var maxProb = probs.Length > 0 ? probs.Max() : 0.5;

                complaintAdvisory = new ComplaintAdvisoryPredictionDto(
                    HasComplaint: true,
                    PredictedCategory: cmpl.PredictedCategory,
                    ModelProbability: maxProb,
                    ReviewStatus: "UnderReview",
                    ModelVersion: cmpl.ModelVersion);
            }

            var assessmentId = (comp?.Id ?? scrn?.AssessmentId ?? cmpl!.AssessmentId).ToString();
            var evaluatedAt = comp?.EvaluationTimestamp
                ?? scrn?.CreatedAtUtc.UtcDateTime
                ?? cmpl!.AssessedAtUtc.UtcDateTime;

            cases.Add(new GovernanceCaseAssessmentDto(
                AssessmentId: assessmentId,
                WorkflowRunId: scrn?.WorkflowRunId?.ToString(),
                CaseId: cId,
                ParcelReference: "Unavailable",
                ApplicantReference: "Unavailable",
                OverallStatus: overallStatus,
                ComplianceStatus: compStatus,
                ConflictStatus: confStatus,
                EarlyScreeningStatus: earlyStatus,
                AnomalyStatus: "NotAssessed",
                ConsensusOutcome: "NotAssessed",
                EvaluatedAt: evaluatedAt.ToString("o"),
                Version: "Persisted Component 4 records",
                SummaryFinding: "Computed governance intelligence findings persisted in PostgreSQL development database.",
                Rationale: "Evaluated by real .NET governance engines against regulatory and administrative policies.",
                RecommendedNextStep: "Review governance dossier in Component 4 management console.",
                ComplaintAdvisory: complaintAdvisory
            ));
        }

        var summary = new GovernanceDashboardSummaryDto(
            TotalCases: cases.Count,
            TotalAssessments: cases.Count,
            ClearCount: clearCount,
            ReviewRequiredCount: reviewCount,
            InsufficientInfoCount: insufficientCount,
            LastUpdated: DateTime.UtcNow.ToString("o"));

        return new GovernanceDashboardDto(summary, cases);
    }
}
