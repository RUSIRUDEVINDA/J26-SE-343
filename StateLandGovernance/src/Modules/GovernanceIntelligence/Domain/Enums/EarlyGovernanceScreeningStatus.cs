namespace StateLandGovernance.GovernanceIntelligence.Domain.Enums;

/// <summary>
/// Represents the overall synthesis outcome of early governance screening.
/// <para>
/// Boundary note: A status of <see cref="Clear"/> denotes solely that no review indicators were identified
/// from the supplied verified evidence. It does NOT signify legal compliance, lease feasibility approval,
/// applicant statutory eligibility, or the definitive absence of all possible land disputes.
/// </para>
/// </summary>
public enum EarlyGovernanceScreeningStatus
{
    /// <summary>
    /// No review indicator was identified from the supplied verified evidence, and all indicators have complete evidence.
    /// </summary>
    Clear = 1,

    /// <summary>
    /// One or more indicators have verified concerns requiring officer administrative review.
    /// </summary>
    ReviewRequired = 2,

    /// <summary>
    /// No verified concerns were flagged, but one or more indicators have unverified, missing, or unavailable evidence.
    /// </summary>
    InsufficientInformation = 3
}
