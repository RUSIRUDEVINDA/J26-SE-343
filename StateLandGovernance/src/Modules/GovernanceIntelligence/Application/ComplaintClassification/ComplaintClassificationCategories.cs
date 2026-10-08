namespace StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;

/// <summary>
/// Authoritative category taxonomy for the complaint classifier contract.
/// </summary>
public static class ComplaintClassificationCategories
{
    public const string AdministrativeProceduralIntegrity = "Administrative / Procedural / Integrity";
    public const string LeaseRevenuePaymentEnforcement = "Lease Revenue / Payment / Enforcement";
    public const string UnauthorizedAllocationTransferUse = "Unauthorized Allocation / Transfer / Use";
    public const string ProtectedEnvironmentalLeaseMisuse = "Protected / Environmental Lease Misuse";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
    [
        AdministrativeProceduralIntegrity,
        LeaseRevenuePaymentEnforcement,
        UnauthorizedAllocationTransferUse,
        ProtectedEnvironmentalLeaseMisuse
    ]);

    public static bool Contains(string? category) => category is
        AdministrativeProceduralIntegrity or
        LeaseRevenuePaymentEnforcement or
        UnauthorizedAllocationTransferUse or
        ProtectedEnvironmentalLeaseMisuse;
}
