using System.Net.Mail;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.LeaseInterest;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.Validators;

/// <summary>
/// Validates lease-interest drafts.
/// <para>
/// <c>forSubmit: false</c> (save draft) accepts incomplete forms: only values the applicant has actually
/// entered are checked for format, range and length.
/// </para>
/// <para>
/// <c>forSubmit: true</c> requires every mandatory field and enforces contact format, requested area,
/// requested term, purpose and consent.
/// </para>
/// </summary>
public sealed class LeaseInterestDraftValidator
{
    public const int MaxLegalNameLength = 200;
    public const int MaxContactPhoneLength = 32;
    public const int MaxContactEmailLength = 254;
    public const int MaxPostalAddressLength = 500;
    public const int MaxOrganizationNameLength = 200;
    public const int MaxOrganizationRegistrationNumberLength = 64;
    public const int MaxPlannedActivitiesLength = 4000;
    public const int MaxDesiredStartPeriodLength = 100;
    public const int MaxProjectSummaryLength = 4000;
    public const int MaxSupportingNotesLength = 4000;

    private const int MinPhoneDigits = 7;
    private const int MaxPhoneDigits = 15;

    private readonly LeaseInterestOptions _options;

    public LeaseInterestDraftValidator(LeaseInterestOptions options)
    {
        _options = options;
    }

    public IReadOnlyList<string> Validate(UpsertLeaseInterestDraftDto draft, bool forSubmit)
    {
        var errors = new List<string>();
        var applicant = draft.Applicant;
        var proposal = draft.Proposal;
        var isOrganization = applicant.Kind == LeaseInterestApplicantKindDto.Organization;

        if (!Enum.IsDefined(typeof(LeaseInterestApplicantKindDto), applicant.Kind))
        {
            errors.Add("Applicant type is not supported.");
        }

        ValidateText(errors, applicant.LegalName, "Applicant Full name", MaxLegalNameLength, forSubmit);
        ValidateText(errors, applicant.PostalAddress, "Postal address", MaxPostalAddressLength, forSubmit);
        ValidateEmail(errors, applicant.ContactEmail, forSubmit);
        ValidatePhone(errors, applicant.ContactPhone, forSubmit);

        if (isOrganization)
        {
            ValidateText(
                errors,
                applicant.OrganizationName,
                "Organization name",
                MaxOrganizationNameLength,
                forSubmit,
                " for organization applicants");
            ValidateText(
                errors,
                applicant.OrganizationRegistrationNumber,
                "Organization registration number",
                MaxOrganizationRegistrationNumberLength,
                forSubmit,
                " for organization applicants");
        }

        ValidateNic(errors, applicant, isOrganization, forSubmit);
        ValidateProposal(errors, proposal, forSubmit);

        ValidateText(
            errors,
            draft.Supporting?.SupportingNotes,
            "Supporting notes",
            MaxSupportingNotesLength,
            required: false);

        if (forSubmit && !draft.ConsentToPreliminaryAssessment)
        {
            errors.Add("Consent to submit for preliminary assessment is required.");
        }

        return errors;
    }

    private void ValidateProposal(List<string> errors, LeaseInterestProposalDto proposal, bool forSubmit)
    {
        // LandUseType has no "unset" member; the default value (0) means no purpose has been chosen yet.
        // Research demo (and Find Suitable Land) only support Agricultural, Residential, Commercial.
        var purposeEntered = (int)proposal.RequestedPurpose != 0;
        if (purposeEntered || forSubmit)
        {
            if (proposal.RequestedPurpose is not (
                LandUseType.Agricultural or
                LandUseType.Residential or
                LandUseType.Commercial))
            {
                errors.Add(
                    "Requested purpose must be Agricultural, Residential, or Commercial.");
            }
        }

        ValidateText(
            errors,
            proposal.PlannedActivitiesDescription,
            "A description of planned activities",
            MaxPlannedActivitiesLength,
            forSubmit,
            requiredMessage: "A description of planned activities is required.");
        ValidateText(
            errors,
            proposal.DesiredStartPeriod,
            "Desired start period",
            MaxDesiredStartPeriodLength,
            forSubmit);
        ValidateText(
            errors,
            proposal.ProjectSummary,
            "Project summary",
            MaxProjectSummaryLength,
            forSubmit);

        var duration = proposal.RequestedDurationYears;
        var durationEntered = duration != 0;
        if ((forSubmit || durationEntered)
            && (duration < _options.MinRequestedDurationYears || duration > _options.MaxRequestedDurationYears))
        {
            errors.Add(
                $"Requested duration must be between {_options.MinRequestedDurationYears} and {_options.MaxRequestedDurationYears} years (requested term only — not an approved lease length).");
        }

        var area = proposal.RequestedAreaHectares;
        if (area is < 0)
        {
            errors.Add("Requested area cannot be negative.");
        }
        else if (forSubmit && (area is null || area == 0m))
        {
            errors.Add("Requested area must be greater than zero.");
        }
    }

    private static void ValidateNic(
        List<string> errors,
        LeaseInterestApplicantDto applicant,
        bool isOrganization,
        bool forSubmit)
    {
        var nicRaw = LeaseInterestApplicantNic.SelectRawNic(applicant);
        var nicEntered = !string.IsNullOrWhiteSpace(nicRaw);

        if (!forSubmit)
        {
            if (nicEntered && !SriLankanNic.TryValidate(nicRaw, out _, out var draftNicError))
            {
                errors.Add(draftNicError!);
            }

            return;
        }

        if (!isOrganization)
        {
            if (!SriLankanNic.TryValidate(nicRaw, out _, out var nicError))
            {
                errors.Add(nicError!);
            }
        }
        else if (!nicEntered)
        {
            errors.Add("Representative National Identity Card (NIC) number is required.");
        }
        else if (!SriLankanNic.TryValidate(nicRaw, out _, out var repNicError))
        {
            errors.Add(repNicError!);
        }
    }

    private static void ValidateEmail(List<string> errors, string? email, bool forSubmit)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            if (forSubmit)
            {
                errors.Add("Contact email is required.");
            }

            return;
        }

        var trimmed = email.Trim();
        if (trimmed.Length > MaxContactEmailLength)
        {
            errors.Add($"Contact email must be {MaxContactEmailLength} characters or fewer.");
            return;
        }

        if (!IsPlausibleEmail(trimmed))
        {
            errors.Add("Contact email must be a valid email address.");
        }
    }

    private static void ValidatePhone(List<string> errors, string? phone, bool forSubmit)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            if (forSubmit)
            {
                errors.Add("Contact phone is required.");
            }

            return;
        }

        var trimmed = phone.Trim();
        if (trimmed.Length > MaxContactPhoneLength)
        {
            errors.Add($"Contact phone must be {MaxContactPhoneLength} characters or fewer.");
            return;
        }

        if (!IsPlausiblePhone(trimmed))
        {
            errors.Add("Contact phone must be a valid phone number (7–15 digits; optional +, spaces, dashes or brackets).");
        }
    }

    private static void ValidateText(
        List<string> errors,
        string? value,
        string label,
        int maxLength,
        bool required,
        string requiredSuffix = "",
        string? requiredMessage = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                errors.Add(requiredMessage ?? $"{label} is required{requiredSuffix}.");
            }

            return;
        }

        if (value.Trim().Length > maxLength)
        {
            errors.Add($"{label} must be {maxLength} characters or fewer.");
        }
    }

    private static bool IsPlausibleEmail(string value)
    {
        if (value.Any(char.IsWhiteSpace) || !MailAddress.TryCreate(value, out var address))
        {
            return false;
        }

        if (!string.Equals(address.Address, value, StringComparison.Ordinal))
        {
            return false;
        }

        var domain = address.Host;
        return domain.Contains('.', StringComparison.Ordinal)
               && !domain.StartsWith('.')
               && !domain.EndsWith('.');
    }

    private static bool IsPlausiblePhone(string value)
    {
        var digits = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsAsciiDigit(c))
            {
                digits++;
                continue;
            }

            if (c == '+' && i == 0)
            {
                continue;
            }

            if (c is ' ' or '-' or '(' or ')')
            {
                continue;
            }

            return false;
        }

        return digits is >= MinPhoneDigits and <= MaxPhoneDigits;
    }
}
