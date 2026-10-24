namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

/// <summary>
/// Relational representation of one complaint-classification assessment snapshot.
/// </summary>
public sealed class ComplaintClassificationAssessmentEntity
{
    public Guid AssessmentId { get; set; }
    public string CaseId { get; set; } = string.Empty;
    public string ComplaintText { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public string PredictedCategory { get; set; } = string.Empty;
    public decimal AdministrativeProceduralIntegrityProbability { get; set; }
    public decimal LeaseRevenuePaymentEnforcementProbability { get; set; }
    public decimal UnauthorizedAllocationTransferUseProbability { get; set; }
    public decimal ProtectedEnvironmentalLeaseMisuseProbability { get; set; }
    public string AdvisoryNote { get; set; } = string.Empty;
    public string ClosedSetNote { get; set; } = string.Empty;
    public DateTimeOffset AssessedAtUtc { get; set; }
}
