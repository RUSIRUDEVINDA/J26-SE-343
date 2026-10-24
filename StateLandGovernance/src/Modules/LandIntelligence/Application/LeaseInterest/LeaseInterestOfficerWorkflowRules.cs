using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.LeaseInterest;

/// <summary>
/// Two-officer handoff: verification officer → valuation officer → authority.
/// </summary>
public static class LeaseInterestOfficerWorkflowRules
{
    public const string VerifyingOfficerCannotValueMessage =
        "The officer who completed preliminary verification cannot record the valuation for the same request. " +
        "Sign in as a different officer to record the worksheet.";

    public const string ValuationReferralNote =
        "Officer valuation recorded; referred to authority for preliminary decision (not a lease or land reservation).";

    public static bool IsVerificationQueueStatus(LeaseInterestRequestStatus status) =>
        status is LeaseInterestRequestStatus.PendingVerification
            or LeaseInterestRequestStatus.NeedsInformation;

    public static bool IsValuationQueueCandidate(
        LeaseInterestRequestStatus status,
        bool hasValuation) =>
        !hasValuation
        && (status == LeaseInterestRequestStatus.AwaitingValuation
            || status == LeaseInterestRequestStatus.ReferredToAuthority);

    public static bool CanOfficerRecordValuation(
        LeaseInterestRequestStatus status,
        bool hasValuation,
        Guid? verificationCompletedByOfficerId,
        Guid valuationOfficerId)
    {
        if (hasValuation)
        {
            return false;
        }

        if (!IsValuationQueueCandidate(status, hasValuation))
        {
            return false;
        }

        return true;
    }
}
