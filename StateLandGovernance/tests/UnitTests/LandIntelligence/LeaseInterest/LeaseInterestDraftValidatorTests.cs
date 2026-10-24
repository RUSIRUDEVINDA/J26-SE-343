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
                1.5m,
                "2027-Q1",
                10,
                "Pilot commercial use"),
            new LeaseInterestSupportingInfoDto("Notes"),
            consent);

    /// <summary>A freshly created draft: blank text, no purpose chosen, no term, no area, no consent.</summary>
    private static UpsertLeaseInterestDraftDto EmptyDraft() =>
        new(
            new LeaseInterestApplicantDto(
                LeaseInterestApplicantKindDto.Individual,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                null),
            new LeaseInterestProposalDto(
                default,
                string.Empty,
                null,
                string.Empty,
                0,
                string.Empty),
            new LeaseInterestSupportingInfoDto(string.Empty),
            false);

    [Fact]
    public void Validate_DraftAllowsCompletelyEmptyForm()
    {
        var errors = CreateValidator().Validate(EmptyDraft(), forSubmit: false);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_DraftAllowsPartialEntry()
    {
        var draft = EmptyDraft() with
        {
            Applicant = EmptyDraft().Applicant with { LegalName = "Ada Lovelace" },
            Proposal = EmptyDraft().Proposal with { PlannedActivitiesDescription = "Retail" },
        };

        var errors = CreateValidator().Validate(draft, forSubmit: false);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_DraftAllowsWhitespaceOnlyFields()
    {
        var draft = EmptyDraft() with
        {
            Applicant = EmptyDraft().Applicant with { LegalName = "   ", ContactEmail = "  ", ContactPhone = "\t" },
        };

        var errors = CreateValidator().Validate(draft, forSubmit: false);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_DraftAllowsOrganizationWithoutOrganizationDetails()
    {
        var draft = EmptyDraft() with
        {
            Applicant = EmptyDraft().Applicant with { Kind = LeaseInterestApplicantKindDto.Organization },
        };

        var errors = CreateValidator().Validate(draft, forSubmit: false);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_DraftRejectsInvalidNicWhenProvided()
    {
        var draft = EmptyDraft() with
        {
            Applicant = EmptyDraft().Applicant with { NationalIdentityCardNumber = "bad" },
        };

        var errors = CreateValidator().Validate(draft, forSubmit: false);

        Assert.Contains(errors, e => e.Contains("valid Sri Lankan NIC"));
    }

    [Fact]
    public void Validate_DraftRejectsNegativeAreaWhenProvided()
    {
        var draft = EmptyDraft() with
        {
            Proposal = EmptyDraft().Proposal with { RequestedAreaHectares = -1m },
        };

        var errors = CreateValidator().Validate(draft, forSubmit: false);

        Assert.Contains(errors, e => e.Contains("cannot be negative"));
    }

    [Fact]
    public void Validate_DraftRejectsUndefinedPurposeWhenProvided()
    {
        var draft = EmptyDraft() with
        {
            Proposal = EmptyDraft().Proposal with { RequestedPurpose = (LandUseType)42 },
        };

        var errors = CreateValidator().Validate(draft, forSubmit: false);

        Assert.Contains(errors, e => e.Contains("Agricultural, Residential, or Commercial"));
    }

    [Fact]
    public void Validate_DraftRejectsUnsupportedPurposeWhenProvided()
    {
        var draft = EmptyDraft() with
        {
            Proposal = EmptyDraft().Proposal with { RequestedPurpose = LandUseType.Industrial },
        };

        var errors = CreateValidator().Validate(draft, forSubmit: false);

        Assert.Contains(errors, e => e.Contains("Agricultural, Residential, or Commercial"));
    }

    [Fact]
    public void Validate_DraftRejectsOutOfRangeDurationWhenProvided()
    {
        var draft = EmptyDraft() with
        {
            Proposal = EmptyDraft().Proposal with { RequestedDurationYears = 99 },
        };

        var errors = CreateValidator().Validate(draft, forSubmit: false);

        Assert.Contains(errors, e => e.Contains("Requested duration must be between"));
    }

    [Fact]
    public void Validate_DraftAppliesLengthLimitsToEnteredText()
    {
        var draft = EmptyDraft() with
        {
            Applicant = EmptyDraft().Applicant with
            {
                LegalName = new string('a', LeaseInterestDraftValidator.MaxLegalNameLength + 1),
            },
            Proposal = EmptyDraft().Proposal with
            {
                ProjectSummary = new string('b', LeaseInterestDraftValidator.MaxProjectSummaryLength + 1),
            },
        };

        var errors = CreateValidator().Validate(draft, forSubmit: false);

        Assert.Contains(errors, e => e.Contains("Applicant Full name must be"));
        Assert.Contains(errors, e => e.Contains("Project summary must be"));
    }

    [Fact]
    public void Validate_DraftRejectsMalformedContactWhenProvided()
    {
        var draft = EmptyDraft() with
        {
            Applicant = EmptyDraft().Applicant with { ContactEmail = "not-an-email", ContactPhone = "abc" },
        };

        var errors = CreateValidator().Validate(draft, forSubmit: false);

        Assert.Contains(errors, e => e.Contains("valid email"));
        Assert.Contains(errors, e => e.Contains("valid phone"));
    }

    [Fact]
    public void Validate_SubmitAcceptsCompleteDraft()
    {
        var errors = CreateValidator().Validate(ValidDraft(), forSubmit: true);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_SubmitRequiresMandatoryFields()
    {
        var errors = CreateValidator().Validate(EmptyDraft(), forSubmit: true);

        Assert.Contains(errors, e => e.Contains("Applicant Full name is required"));
        Assert.Contains(errors, e => e.Contains("Contact phone is required"));
        Assert.Contains(errors, e => e.Contains("Contact email is required"));
        Assert.Contains(errors, e => e.Contains("Postal address is required"));
        Assert.Contains(errors, e => e.Contains("planned activities is required"));
        Assert.Contains(errors, e => e.Contains("Desired start period is required"));
        Assert.Contains(errors, e => e.Contains("Project summary is required"));
        Assert.Contains(errors, e => e.Contains("Agricultural, Residential, or Commercial"));
        Assert.Contains(errors, e => e.Contains("Requested duration must be between"));
        Assert.Contains(errors, e => e.Contains("Requested area must be greater than zero"));
        Assert.Contains(errors, e => e.Contains("Consent"));
        Assert.Contains(errors, e => e.Contains("NIC"));
    }

    [Fact]
    public void Validate_SubmitRejectsWhitespaceOnlyFields()
    {
        var valid = ValidDraft();
        var draft = valid with
        {
            Applicant = valid.Applicant with { LegalName = "   ", PostalAddress = "\t" },
            Proposal = valid.Proposal with { ProjectSummary = "  " },
        };

        var errors = CreateValidator().Validate(draft, forSubmit: true);

        Assert.Contains(errors, e => e.Contains("Applicant Full name is required"));
        Assert.Contains(errors, e => e.Contains("Postal address is required"));
        Assert.Contains(errors, e => e.Contains("Project summary is required"));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@domain")]
    [InlineData("two words@example.com")]
    public void Validate_SubmitRejectsInvalidEmail(string email)
    {
        var valid = ValidDraft();
        var draft = valid with { Applicant = valid.Applicant with { ContactEmail = email } };

        var errors = CreateValidator().Validate(draft, forSubmit: true);

        Assert.Contains(errors, e => e.Contains("valid email"));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("+94 77 123 4567 ext 9")]
    public void Validate_SubmitRejectsInvalidPhone(string phone)
    {
        var valid = ValidDraft();
        var draft = valid with { Applicant = valid.Applicant with { ContactPhone = phone } };

        var errors = CreateValidator().Validate(draft, forSubmit: true);

        Assert.Contains(errors, e => e.Contains("valid phone") || e.Contains("characters or fewer"));
    }

    [Theory]
    [InlineData("+94 77 123 4567")]
    [InlineData("0771234567")]
    [InlineData("(011) 234-5678")]
    public void Validate_SubmitAcceptsCommonPhoneFormats(string phone)
    {
        var valid = ValidDraft();
        var draft = valid with { Applicant = valid.Applicant with { ContactPhone = phone } };

        var errors = CreateValidator().Validate(draft, forSubmit: true);

        Assert.DoesNotContain(errors, e => e.Contains("phone"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-2)]
    public void Validate_SubmitRequiresPositiveArea(int? area)
    {
        var valid = ValidDraft();
        var draft = valid with { Proposal = valid.Proposal with { RequestedAreaHectares = area } };

        var errors = CreateValidator().Validate(draft, forSubmit: true);

        Assert.Contains(errors, e => e.Contains("area"));
    }

    [Fact]
    public void Validate_SubmitRejectsUndefinedPurpose()
    {
        var valid = ValidDraft();
        var draft = valid with { Proposal = valid.Proposal with { RequestedPurpose = (LandUseType)42 } };

        var errors = CreateValidator().Validate(draft, forSubmit: true);

        Assert.Contains(errors, e => e.Contains("Agricultural, Residential, or Commercial"));
    }

    [Fact]
    public void Validate_SubmitRejectsUnsupportedPurpose()
    {
        var valid = ValidDraft();
        var draft = valid with { Proposal = valid.Proposal with { RequestedPurpose = LandUseType.Other } };

        var errors = CreateValidator().Validate(draft, forSubmit: true);

        Assert.Contains(errors, e => e.Contains("Agricultural, Residential, or Commercial"));
    }

    [Fact]
    public void Validate_SubmitOrganizationRequiresOrganizationDetailsAndBrn()
    {
        var valid = ValidDraft();
        var draft = valid with
        {
            Applicant = valid.Applicant with
            {
                Kind = LeaseInterestApplicantKindDto.Organization,
                NationalIdentityCardNumber = null,
                OrganizationName = "Ceylonova Global",
                OrganizationRegistrationNumber = "B11223",
            },
        };

        var errors = CreateValidator().Validate(draft, forSubmit: true);

        Assert.DoesNotContain(errors, e => e.Contains("Representative National Identity Card"));
    }

    [Fact]
    public void Validate_SubmitOrganizationRejectsInvalidBrn()
    {
        var valid = ValidDraft();
        var draft = valid with
        {
            Applicant = valid.Applicant with
            {
                Kind = LeaseInterestApplicantKindDto.Organization,
                NationalIdentityCardNumber = null,
                OrganizationName = "Ceylonova Global",
                OrganizationRegistrationNumber = "!!",
            },
        };

        var errors = CreateValidator().Validate(draft, forSubmit: true);

        Assert.Contains(errors, e => e.Contains("business registration number"));
    }

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
    public void Validate_SubmitRejectsPastDesiredStartPeriod()
    {
        var valid = ValidDraft();
        var draft = valid with
        {
            Proposal = valid.Proposal with { DesiredStartPeriod = "2000-01-01" },
        };

        var errors = CreateValidator().Validate(draft, forSubmit: true);

        Assert.Contains(errors, e => e.Contains("Desired start period cannot be in the past"));
    }

    [Fact]
    public void Validate_SubmitRequiresConsent()
    {
        var errors = CreateValidator().Validate(ValidDraft(consent: false), forSubmit: true);

        Assert.Contains(errors, e => e.Contains("Consent"));
    }

    [Fact]
    public void Validate_DraftDoesNotRequireConsent()
    {
        var errors = CreateValidator().Validate(ValidDraft(consent: false), forSubmit: false);

        Assert.Empty(errors);
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
