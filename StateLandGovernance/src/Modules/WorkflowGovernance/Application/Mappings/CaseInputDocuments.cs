namespace StateLandGovernance.WorkflowGovernance.Application.Mappings;
/// <summary>Workflow outputs do not retroactively become inputs to initial completeness/screening.</summary>
public static class CaseInputDocuments
{
    public static bool IsIntakeInput(string category) => !new[] { "WorkflowApproval:", "WorkflowEvidence:", "ProjectFulfillment:" }
        .Any(prefix => category.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
