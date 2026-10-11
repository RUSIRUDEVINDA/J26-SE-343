using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL-backed append/read store for complaint-classification assessment history.
/// </summary>
public sealed class PostgresComplaintClassificationAssessmentStore
    : IComplaintClassificationAssessmentStore
{
    private const double ProbabilitySumTolerance = 1e-6;
    private readonly GovernanceIntelligenceDbContext _dbContext;

    public PostgresComplaintClassificationAssessmentStore(GovernanceIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task AddAsync(
        ComplaintClassificationAssessment assessment,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(assessment);

        var entity = new ComplaintClassificationAssessmentEntity
        {
            AssessmentId = assessment.AssessmentId,
            CaseId = assessment.CaseId,
            ComplaintText = assessment.ComplaintText,
            ModelVersion = assessment.ModelVersion,
            PredictedCategory = assessment.PredictedCategory,
            AdministrativeProceduralIntegrityProbability = GetProbability(
                assessment,
                ComplaintClassificationCategories.AdministrativeProceduralIntegrity),
            LeaseRevenuePaymentEnforcementProbability = GetProbability(
                assessment,
                ComplaintClassificationCategories.LeaseRevenuePaymentEnforcement),
            UnauthorizedAllocationTransferUseProbability = GetProbability(
                assessment,
                ComplaintClassificationCategories.UnauthorizedAllocationTransferUse),
            ProtectedEnvironmentalLeaseMisuseProbability = GetProbability(
                assessment,
                ComplaintClassificationCategories.ProtectedEnvironmentalLeaseMisuse),
            AdvisoryNote = assessment.AdvisoryNote,
            ClosedSetNote = assessment.ClosedSetNote,
            AssessedAtUtc = assessment.AssessedAtUtc
        };

        await _dbContext.ComplaintClassificationAssessments.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ComplaintClassificationAssessment?> GetByIdAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (assessmentId == Guid.Empty)
        {
            throw new ArgumentException("AssessmentId cannot be empty.", nameof(assessmentId));
        }

        var entity = await _dbContext.ComplaintClassificationAssessments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.AssessmentId == assessmentId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        var probabilities = new ReadOnlyDictionary<string, double>(
            new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [ComplaintClassificationCategories.AdministrativeProceduralIntegrity] =
                    (double)entity.AdministrativeProceduralIntegrityProbability,
                [ComplaintClassificationCategories.LeaseRevenuePaymentEnforcement] =
                    (double)entity.LeaseRevenuePaymentEnforcementProbability,
                [ComplaintClassificationCategories.UnauthorizedAllocationTransferUse] =
                    (double)entity.UnauthorizedAllocationTransferUseProbability,
                [ComplaintClassificationCategories.ProtectedEnvironmentalLeaseMisuse] =
                    (double)entity.ProtectedEnvironmentalLeaseMisuseProbability
            });

        return new ComplaintClassificationAssessment(
            entity.AssessmentId,
            entity.CaseId,
            entity.ComplaintText,
            entity.ModelVersion,
            entity.PredictedCategory,
            probabilities,
            entity.AdvisoryNote,
            entity.ClosedSetNote,
            entity.AssessedAtUtc);
    }

    private static decimal GetProbability(
        ComplaintClassificationAssessment assessment,
        string category)
    {
        var roundTripValue = assessment.ClassProbabilities[category]
            .ToString("R", CultureInfo.InvariantCulture);
        return decimal.Parse(roundTripValue, CultureInfo.InvariantCulture);
    }

    private static void Validate(ComplaintClassificationAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        if (assessment.AssessmentId == Guid.Empty)
        {
            throw new ArgumentException("AssessmentId cannot be empty.", nameof(assessment));
        }

        if (string.IsNullOrWhiteSpace(assessment.CaseId) ||
            assessment.CaseId.Length > ComplaintClassificationConstraints.MaximumCaseIdLength)
        {
            throw new ArgumentException(
                $"CaseId is required and must not exceed {ComplaintClassificationConstraints.MaximumCaseIdLength} characters.",
                nameof(assessment));
        }

        if (string.IsNullOrWhiteSpace(assessment.ComplaintText) ||
            assessment.ComplaintText.EnumerateRunes().Count() > ComplaintClassificationConstraints.MaximumComplaintTextLength)
        {
            throw new ArgumentException(
                $"ComplaintText is required and must not exceed {ComplaintClassificationConstraints.MaximumComplaintTextLength} Unicode characters.",
                nameof(assessment));
        }

        if (string.IsNullOrWhiteSpace(assessment.ModelVersion))
        {
            throw new ArgumentException("ModelVersion cannot be blank.", nameof(assessment));
        }

        if (!ComplaintClassificationCategories.Contains(assessment.PredictedCategory))
        {
            throw new ArgumentException("PredictedCategory is not in the closed classifier taxonomy.", nameof(assessment));
        }

        if (string.IsNullOrWhiteSpace(assessment.AdvisoryNote) || string.IsNullOrWhiteSpace(assessment.ClosedSetNote))
        {
            throw new ArgumentException("Classifier advisory notes cannot be blank.", nameof(assessment));
        }

        if (assessment.AssessedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("AssessedAtUtc must have a zero UTC offset.", nameof(assessment));
        }

        if (assessment.ClassProbabilities is null ||
            assessment.ClassProbabilities.Count != ComplaintClassificationCategories.All.Count ||
            ComplaintClassificationCategories.All.Any(category =>
                !assessment.ClassProbabilities.TryGetValue(category, out var probability) ||
                !double.IsFinite(probability) ||
                probability < 0 ||
                probability > 1) ||
            assessment.ClassProbabilities.Keys.Any(category => !ComplaintClassificationCategories.Contains(category)))
        {
            throw new ArgumentException(
                "ClassProbabilities must contain exactly the four taxonomy categories with finite values between zero and one.",
                nameof(assessment));
        }

        if (Math.Abs(assessment.ClassProbabilities.Values.Sum() - 1.0) > ProbabilitySumTolerance)
        {
            throw new ArgumentException("ClassProbabilities must sum to one.", nameof(assessment));
        }
    }
}
