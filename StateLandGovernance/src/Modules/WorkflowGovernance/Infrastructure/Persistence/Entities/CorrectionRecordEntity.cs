namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

public sealed class CorrectionRecordEntity
{
    public Guid Id { get; set; }
    public Guid LeaseCaseId { get; set; }
    public int Revision { get; set; }
    public string RecordJson { get; set; } = string.Empty;
}
