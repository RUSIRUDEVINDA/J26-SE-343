namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

/// <summary>
/// Application boundary input for lease proposal intake details.
/// Uses primitive representations to keep the Presentation boundary clean.
/// </summary>
public sealed record ProposalIntakeInput(
    string? Purpose,
    decimal? RequestedExtentValue,
    string? RequestedExtentUnit,
    string? JurisdictionCode,
    string? Region,
    string? District,
    string SourceReference,
    string SourceVersion)
{
    public static ProposalIntakeInput FromDomain(LeaseProposalIntake intake)
    {
        if (intake == null)
        {
            throw new ArgumentNullException(nameof(intake));
        }

        return new ProposalIntakeInput(
            intake.Purpose?.Value,
            intake.RequestedExtent?.Value,
            intake.RequestedExtent?.Unit.Name,
            intake.Jurisdiction?.Code,
            intake.Jurisdiction?.Region,
            intake.Jurisdiction?.District,
            intake.SourceReference.Reference,
            intake.SourceReference.Version);
    }

    public LeaseProposalIntake ToDomain()
    {
        LeasePurpose? purpose = !string.IsNullOrWhiteSpace(Purpose) ? new LeasePurpose(Purpose) : null;

        LandExtent? extent = null;
        if (RequestedExtentValue.HasValue)
        {
            var unit = !string.IsNullOrWhiteSpace(RequestedExtentUnit)
                ? new MeasurementUnit(RequestedExtentUnit)
                : MeasurementUnit.Acre;
            extent = new LandExtent(RequestedExtentValue.Value, unit);
        }

        JurisdictionContext? jurisdiction = !string.IsNullOrWhiteSpace(JurisdictionCode)
            ? new JurisdictionContext(JurisdictionCode, Region, District)
            : null;

        var sourceRef = new IntakeSourceReference(SourceReference, SourceVersion);

        return new LeaseProposalIntake(purpose, extent, jurisdiction, sourceRef);
    }
}
