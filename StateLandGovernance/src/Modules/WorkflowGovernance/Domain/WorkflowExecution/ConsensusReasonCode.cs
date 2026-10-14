namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

public readonly record struct ConsensusReasonCode
{
    public string Value { get; }
    
    public ConsensusReasonCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new System.ArgumentException("ConsensusReasonCode cannot be empty.", nameof(value));
        Value = value;
    }
}
