using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Application.ProjectLeaseReady;
using ValidationException = StateLandGovernance.LandIntelligence.Application.Interfaces.ValidationException;

namespace StateLandGovernance.LandIntelligence.Application.Services;

public sealed class LandParcelAvailabilityComposer
{
    private readonly IProjectLeaseReadyDesignationReadRepository _projectLeaseReady;
    private readonly IResearchSimulatedLeaseReadRepository _simulatedLease;
    private readonly IResearchDemoMode _researchDemoMode;
    private readonly IRealParcelSourceReadRepository _realParcelSources;

    public LandParcelAvailabilityComposer(
        IProjectLeaseReadyDesignationReadRepository projectLeaseReady,
        IResearchSimulatedLeaseReadRepository simulatedLease,
        IResearchDemoMode researchDemoMode,
        IRealParcelSourceReadRepository realParcelSources)
    {
        _projectLeaseReady = projectLeaseReady;
        _simulatedLease = simulatedLease;
        _researchDemoMode = researchDemoMode;
        _realParcelSources = realParcelSources;
    }

    public async Task<LandParcelAvailabilityDto> BuildAsync(
        Guid landParcelId,
        CadastralSourceMetadataDto? cadastralSource,
        CancellationToken cancellationToken = default)
    {
        var designation = await _projectLeaseReady.GetByParcelIdAsync(landParcelId, cancellationToken);
        ResearchSimulatedLeaseWorkflowDto? simulated = null;
        if (_researchDemoMode.IsEnabled)
        {
            simulated = await _simulatedLease.GetByParcelIdAsync(landParcelId, cancellationToken);
        }

        return LandParcelAvailabilityMapper.Build(
            cadastralSource,
            simulated,
            _researchDemoMode.IsEnabled,
            designation);
    }

    public async Task<IReadOnlyDictionary<Guid, LandParcelAvailabilityDto>> BuildManyAsync(
        IReadOnlyList<Guid> landParcelIds,
        IReadOnlyDictionary<Guid, CadastralSourceMetadataDto> cadastralSources,
        CancellationToken cancellationToken = default)
    {
        if (landParcelIds.Count == 0)
        {
            return new Dictionary<Guid, LandParcelAvailabilityDto>();
        }

        var designations = await _projectLeaseReady.GetByParcelIdsAsync(landParcelIds, cancellationToken);
        var result = new Dictionary<Guid, LandParcelAvailabilityDto>(landParcelIds.Count);
        foreach (var parcelId in landParcelIds)
        {
            designations.TryGetValue(parcelId, out var designation);
            cadastralSources.TryGetValue(parcelId, out var source);
            result[parcelId] = LandParcelAvailabilityMapper.Build(
                source,
                simulatedWorkflow: null,
                includeSimulatedWorkflow: false,
                designation);
        }

        return result;
    }

    public async Task<LandParcelMetricsDto> BuildMetricsAsync(CancellationToken cancellationToken = default)
    {
        var total = await _projectLeaseReady.CountTotalParcelsAsync(cancellationToken);
        var designated = await _projectLeaseReady.ListDesignatedAsync(cancellationToken);
        var designatedCount = designated.Count;
        if (designatedCount == 0)
        {
            return new LandParcelMetricsDto(total, 0, 0);
        }

        var sources = await _realParcelSources.GetByParcelIdsAsync(
            designated.Select(d => d.LandParcelId).ToList(),
            cancellationToken);

        var effectiveCount = designated.Count(d =>
            !ProjectLeaseReadyDesignationEvaluator.IsSourceReportedPrivate(
                sources.GetValueOrDefault(d.LandParcelId)));

        return new LandParcelMetricsDto(total, designatedCount, effectiveCount);
    }

    public async Task EnsureLeaseInterestPermittedAsync(
        Guid landParcelId,
        CancellationToken cancellationToken = default)
    {
        var source = await _realParcelSources.GetByParcelIdAsync(landParcelId, cancellationToken);
        if (ProjectLeaseReadyDesignationEvaluator.IsSourceReportedPrivate(source))
        {
            throw new ValidationException(
            [
                "Lease interest cannot be submitted for this parcel: source-reported Private claimant category. " +
                "This is preliminary screening only — not a legal ownership determination.",
            ]);
        }

        var designation = await _projectLeaseReady.GetByParcelIdAsync(landParcelId, cancellationToken);
        if (designation is null)
        {
            return;
        }

        var evaluation = ProjectLeaseReadyDesignationEvaluator.Evaluate(designation, source);
        if (!evaluation.IsEffectiveForLeaseFlow)
        {
            throw new ValidationException(
            [
                "Lease interest cannot be submitted for this parcel: it is excluded under project screening " +
                "(source-reported Private claimant category). Hard restrictions and officer verification still apply elsewhere.",
            ]);
        }
    }
}
