using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Validators;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.UnitTests.LandIntelligence.LeaseInterest;

public sealed class LeaseInterestDraftValidatorTests
{
    private static LeaseInterestDraftValidator CreateValidator() =>
        new(new LeaseInterestOptions { MinRequestedDurationYears = 1, MaxRequestedDurationYears = 30 });

    private static UpsertLeaseInterestDraftDto ValidDraft(bool consent = true) =>
        new(
            new LeaseInterestApplicantDto(
                LeaseInterestApplicantKindDto.Individual,
                "Ada Lovelace",
                "+94771234567",
                "ada@example.com",
                "1 Example Road, Colombo",
                null,
                null,
                "123456789V",
                null),
            new LeaseInterestProposalDto(
                LandUseType.Commercial,
                "Retail and community services",
                null,
                "2027-Q1",
                10,
                "Pilot commercial use"),
            new LeaseInterestSupportingInfoDto("Notes"),
            consent);

    [Fact]
    public void Validate_RejectsDurationAbovePolicyMaximum()
    {
        var draft = ValidDraft() with
        {
            Proposal = ValidDraft().Proposal with { RequestedDurationYears = 31 },
        };

        var errors = CreateValidator().Validate(draft, forSubmit: true);

        Assert.Contains(errors, e => e.Contains("Requested duration must be between"));
    }

    [Fact]
    public void Validate_SubmitRequiresConsent()
    {
        var errors = CreateValidator().Validate(ValidDraft(consent: false), forSubmit: true);

        Assert.Contains(errors, e => e.Contains("Consent"));
    }

    [Fact]
    public void Validate_SubmitRequiresValidIndividualNic()
    {
        var draft = ValidDraft() with
        {
            Applicant = ValidDraft().Applicant with { NationalIdentityCardNumber = "bad" },
        };

        var errors = CreateValidator().Validate(draft, forSubmit: true);

        Assert.Contains(errors, e => e.Contains("valid Sri Lankan NIC"));
    }

    [Fact]
    public void Validate_SubmitAcceptsOldNicFormat()
    {
        var draft = ValidDraft() with
        {
            Applicant = ValidDraft().Applicant with { NationalIdentityCardNumber = "123456789V" },
        };

        var errors = CreateValidator().Validate(draft, forSubmit: true);

        Assert.DoesNotContain(errors, e => e.Contains("NIC"));
    }
}
