namespace StateLandGovernance.Api.Security;

// Fictional Development identities only. No case IDs, assignments or capabilities.
public static class DemoAccountProfiles
{
    public const string Password = "password";
    public static Dictionary<string,string?>[] Create() =>
    [
        new() { ["Username"] = "applicant", ["DisplayName"] = "Applicant", ["DisplayRole"] = "Applicant", ["ActorId"] = "8ceaeb44-c735-403b-a226-b514f27287fb", ["TokenRole"] = "Applicant", ["Institution"] = null, ["Home"] = "/workflow-governance/applicant/cases" },
        new() { ["Username"] = "ds", ["DisplayName"] = "Ds", ["DisplayRole"] = "Coordinating / DS officer", ["ActorId"] = "44fe5f4d-c935-531d-9097-2f1a14bdada8", ["TokenRole"] = "Officer", ["Institution"] = "DS", ["Home"] = "/workflow-governance/cases" },
        new() { ["Username"] = "external", ["DisplayName"] = "External", ["DisplayRole"] = "External institutional reviewer", ["ActorId"] = "b59cda29-04ea-5ffd-8ea8-7ab01d977351", ["TokenRole"] = "Officer", ["Institution"] = "DEVELOPMENT_EXTERNAL", ["Home"] = "/workflow-governance/my-requests" },
        new() { ["Username"] = "provincial", ["DisplayName"] = "Provincial", ["DisplayRole"] = "Provincial reviewer", ["ActorId"] = "8c2a9a40-6d99-59a5-a2df-f263b687a25b", ["TokenRole"] = "Officer", ["Institution"] = "PROVINCIAL_LC", ["Home"] = "/workflow-governance/my-requests" },
        new() { ["Username"] = "interprovincial", ["DisplayName"] = "Interprovincial", ["DisplayRole"] = "Inter-provincial reviewer", ["ActorId"] = "f7f7ff41-a4eb-5349-8fff-f5015264f776", ["TokenRole"] = "Officer", ["Institution"] = "INTER_PROVINCIAL_LC", ["Home"] = "/workflow-governance/my-requests" },
        new() { ["Username"] = "commissioner", ["DisplayName"] = "Commissioner", ["DisplayRole"] = "Land Commissioner", ["ActorId"] = "0ab669fe-d0ab-5695-ad55-10f6b3b5aab3", ["TokenRole"] = "Officer", ["Institution"] = "COMMISSIONER", ["Home"] = "/workflow-governance/my-requests" },
        new() { ["Username"] = "research", ["DisplayName"] = "Research", ["DisplayRole"] = "Synthetic screening research only", ["ActorId"] = "d11b53f4-c656-5f5e-9791-969a658813ac", ["TokenRole"] = "Officer", ["Institution"] = "DEVELOPMENT_RESEARCH", ["Home"] = "/component-04" },
        new() { ["Username"] = "reviewtest", ["DisplayName"] = "Reviewtest", ["DisplayRole"] = "Synthetic review test officer", ["ActorId"] = "de4c0000-0000-4000-8000-000000000025", ["TokenRole"] = "Officer", ["Institution"] = "DS", ["Home"] = "/workflow-governance/cases" },
        new() { ["Username"] = "reviewapplicant", ["DisplayName"] = "Reviewapplicant", ["DisplayRole"] = "Synthetic review test applicant", ["ActorId"] = "de4c0000-0000-4000-8000-000000000026", ["TokenRole"] = "Applicant", ["Institution"] = null, ["Home"] = "/workflow-governance/applicant/cases" }
    ];
}
