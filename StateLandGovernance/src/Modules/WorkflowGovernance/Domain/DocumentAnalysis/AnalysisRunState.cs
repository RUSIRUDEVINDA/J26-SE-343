namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

public enum AnalysisRunState
{
    Requested,
    Running,
    Completed,
    Failed,
    Superseded
}
