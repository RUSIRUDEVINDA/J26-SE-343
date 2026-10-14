using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public sealed class GovernanceDashboardReaderTests
{
    [Fact]
    public async Task GetDashboardAsync_WhenDatabaseIsEmpty_ReturnsEmptyDashboardWithoutSyntheticCases()
    {
        await using var context = CreateContext();
        var reader = new GovernanceDashboardReader(context);

        var result = await reader.GetDashboardAsync();

        Assert.Empty(result.Cases);
        Assert.Equal(0, result.Summary.TotalCases);
        Assert.Equal(0, result.Summary.TotalAssessments);
    }

    [Fact]
    public async Task GetDashboardAsync_WhenOnlyComplaintExists_PreservesRealIdentityAndMarksOtherChecksUnassessed()
    {
        await using var context = CreateContext();
        var assessmentId = Guid.NewGuid();
        var assessedAt = new DateTimeOffset(2026, 10, 11, 8, 30, 0, TimeSpan.Zero);
        context.ComplaintClassificationAssessments.Add(new ComplaintClassificationAssessmentEntity
        {
            AssessmentId = assessmentId,
            CaseId = "FICTIONAL-PP1-CASE-001",
            ComplaintText = "Fictional integration complaint.",
            ModelVersion = "v2",
            PredictedCategory = "Administrative / Procedural / Integrity",
            AdministrativeProceduralIntegrityProbability = 0.7m,
            LeaseRevenuePaymentEnforcementProbability = 0.1m,
            UnauthorizedAllocationTransferUseProbability = 0.1m,
            ProtectedEnvironmentalLeaseMisuseProbability = 0.1m,
            AdvisoryNote = "Advisory machine prediction.",
            ClosedSetNote = "Closed-set classification.",
            AssessedAtUtc = assessedAt,
        });
        await context.SaveChangesAsync();
        var reader = new GovernanceDashboardReader(context);

        var result = await reader.GetDashboardAsync();

        var item = Assert.Single(result.Cases);
        Assert.Equal(assessmentId.ToString(), item.AssessmentId);
        Assert.Equal("FICTIONAL-PP1-CASE-001", item.CaseId);
        Assert.Equal("Unavailable", item.ParcelReference);
        Assert.Equal("Unavailable", item.ApplicantReference);
        Assert.Equal("NotAssessed", item.ComplianceStatus);
        Assert.Equal("NotAssessed", item.ConflictStatus);
        Assert.Equal("NotAssessed", item.EarlyScreeningStatus);
        Assert.Equal("NotAssessed", item.AnomalyStatus);
        Assert.Equal("NotAssessed", item.ConsensusOutcome);
        Assert.Equal("InsufficientInformation", item.OverallStatus);
        Assert.Equal("v2", item.ComplaintAdvisory?.ModelVersion);
        Assert.Equal(assessedAt.UtcDateTime.ToString("o"), item.EvaluatedAt);
    }

    private static GovernanceIntelligenceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>()
            .UseInMemoryDatabase($"governance-dashboard-{Guid.NewGuid()}")
            .Options;
        return new GovernanceIntelligenceDbContext(options);
    }
}
