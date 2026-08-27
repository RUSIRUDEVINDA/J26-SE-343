namespace StateLandGovernance.GovernanceIntelligence.Domain.Constants;

/// <summary>
/// Machine-readable stable reason code constants for explainable governance outputs.
/// </summary>
public static class GovernanceReasonCodes
{
    public const string ComplianceRuleViolation = "COMPLIANCE_RULE_VIOLATION";
    public const string ComplianceConditionUnsatisfied = "COMPLIANCE_CONDITION_UNSATISFIED";
    public const string ConflictingGovernanceDecisions = "CONFLICTING_GOVERNANCE_DECISIONS";
    public const string JurisdictionalOverlapDetected = "JURISDICTIONAL_OVERLAP_DETECTED";
    public const string HighGovernanceRisk = "HIGH_GOVERNANCE_RISK";
    public const string RepeatedOverridePattern = "REPEATED_OVERRIDE_PATTERN";
    public const string UnverifiedComplaintConcentration = "UNVERIFIED_COMPLAINT_CONCENTRATION";
    public const string InstitutionalValidationFailure = "INSTITUTIONAL_VALIDATION_FAILURE";
    public const string NoElevatedGovernanceIssues = "NO_ELEVATED_GOVERNANCE_ISSUES";
}
