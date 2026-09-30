namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning.Routing;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

public sealed record InstitutionalRoutingConfig(
    WorkflowRuleSetReference RuleSetReference,
    int StandardStageDurationDays = 14,
    int DivisionalSecretariatDurationDays = 14,
    int CommissionerDurationDays = 7,
    string StandardCapability = "InstitutionalReviewer",
    string DivisionalSecretaryCapability = "DivisionalSecretary",
    string CommissionerCapability = "LandCommissioner")
{
    public static readonly InstitutionalRoutingConfig Default = new(new WorkflowRuleSetReference("STANDARD_ROUTING", "1.0"));
}
