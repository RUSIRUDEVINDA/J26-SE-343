using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;

namespace StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;

/// <summary>
/// No verified valuation or tax basis is available for research cadastre parcels —
/// all monetary lines remain pending official assessment.
/// </summary>
public sealed class LeaseInterestCostAssessmentService : ILeaseInterestCostAssessmentService
{
    private const string Pending = "PendingAuthorityAssessment";

    public LeaseInterestCostSummaryDto BuildCostSummary(Guid landParcelId)
    {
        _ = landParcelId;

        return new LeaseInterestCostSummaryDto(
            Currency: "LKR",
            Disclaimer: "Pending authority assessment — not an invoice or payment obligation.",
            NumericEstimateAvailable: false,
            Lines:
            [
                Line("assessedLandValue", "Assessed land value"),
                Line("upfrontPremium", "Upfront premium (if applicable)"),
                Line("annualLeaseRent", "Annual lease rent"),
                Line("refundableDeposit", "Refundable deposit (if applicable)"),
                Line("stampDuty", "Stamp duty"),
                Line("otherTaxesAndFees", "Other applicable taxes and fees"),
                Line("estimatedInitialPayment", "Estimated initial payment"),
            ]);
    }

    private static LeaseInterestCostLineDto Line(string code, string label) =>
        new(code, label, Pending, null, null, null, null, null);
}
