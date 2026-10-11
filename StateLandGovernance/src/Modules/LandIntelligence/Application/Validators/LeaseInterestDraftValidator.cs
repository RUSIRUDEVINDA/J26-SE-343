using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.Validators;

public sealed class LeaseInterestDraftValidator
{
    private readonly LeaseInterestOptions _options;

    public LeaseInterestDraftValidator(LeaseInterestOptions options)
    {
        _options = options;
    }

    public IReadOnlyList<string> Validate(UpsertLeaseInterestDraftDto draft, bool forSubmit)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(draft.Applicant.LegalName))
        {
            errors.Add("Applicant legal name is required.");
        }

        if (string.IsNullOrWhiteSpace(draft.Applicant.ContactPhone))
        {
            errors.Add("Contact phone is required.");
        }

        if (string.IsNullOrWhiteSpace(draft.Applicant.ContactEmail))
        {
            errors.Add("Contact email is required.");
        }

        if (string.IsNullOrWhiteSpace(draft.Applicant.PostalAddress))
        {
            errors.Add("Postal address is required.");
        }

        if (draft.Applicant.Kind == LeaseInterestApplicantKindDto.Organization)
        {
            if (string.IsNullOrWhiteSpace(draft.Applicant.OrganizationName))
            {
                errors.Add("Organization name is required for organization applicants.");
            }

            if (string.IsNullOrWhiteSpace(draft.Applicant.OrganizationRegistrationNumber))
            {
                errors.Add("Organization registration number is required for organization applicants.");
            }
        }

        if (!Enum.IsDefined(typeof(LandUseType), draft.Proposal.RequestedPurpose))
        {
            errors.Add("Requested purpose is not supported.");
        }

        if (string.IsNullOrWhiteSpace(draft.Proposal.PlannedActivitiesDescription))
        {
            errors.Add("A description of planned activities is required.");
        }

        if (string.IsNullOrWhiteSpace(draft.Proposal.DesiredStartPeriod))
        {
            errors.Add("Desired start period is required.");
        }

        if (string.IsNullOrWhiteSpace(draft.Proposal.ProjectSummary))
        {
            errors.Add("Project summary is required.");
        }

        var duration = draft.Proposal.RequestedDurationYears;
        if (duration < _options.MinRequestedDurationYears || duration > _options.MaxRequestedDurationYears)
        {
            errors.Add(
                $"Requested duration must be between {_options.MinRequestedDurationYears} and {_options.MaxRequestedDurationYears} years (requested term only — not an approved lease length).");
        }

        if (draft.Proposal.RequestedAreaHectares is < 0)
        {
            errors.Add("Requested area cannot be negative.");
        }

        if (forSubmit && !draft.ConsentToPreliminaryAssessment)
        {
            errors.Add("Consent to submit for preliminary assessment is required.");
        }

        return errors;
    }
}
