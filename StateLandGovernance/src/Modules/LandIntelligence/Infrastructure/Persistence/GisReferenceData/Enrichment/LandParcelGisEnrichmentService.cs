using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;

public sealed class LandParcelGisEnrichmentService : ILandParcelGisEnrichmentService
{
    private readonly LandIntelligenceDbContext _dbContext;
    private readonly IAdministrativeLocationVerificationService _administrativeVerificationService;
    private readonly IRoadAccessibilityEnrichmentService _roadAccessibilityEnrichmentService;
    private readonly IWaterProximityEnrichmentService _waterProximityEnrichmentService;
    private readonly ISoilGroupEnrichmentService _soilGroupEnrichmentService;
    private readonly IEnvironmentalSpatialConstraintEnrichmentService _environmentalEnrichmentService;
    private readonly ILogger<LandParcelGisEnrichmentService> _logger;

    public LandParcelGisEnrichmentService(
        LandIntelligenceDbContext dbContext,
        IAdministrativeLocationVerificationService administrativeVerificationService,
        IRoadAccessibilityEnrichmentService roadAccessibilityEnrichmentService,
        IWaterProximityEnrichmentService waterProximityEnrichmentService,
        ISoilGroupEnrichmentService soilGroupEnrichmentService,
        IEnvironmentalSpatialConstraintEnrichmentService environmentalEnrichmentService,
        ILogger<LandParcelGisEnrichmentService> logger)
    {
        _dbContext = dbContext;
        _administrativeVerificationService = administrativeVerificationService;
        _roadAccessibilityEnrichmentService = roadAccessibilityEnrichmentService;
        _waterProximityEnrichmentService = waterProximityEnrichmentService;
        _soilGroupEnrichmentService = soilGroupEnrichmentService;
        _environmentalEnrichmentService = environmentalEnrichmentService;
        _logger = logger;
    }

    public async Task<LandParcelGisEnrichmentResult> EnrichAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default)
    {
        var parcelIdentity = await _dbContext.LandParcels
            .AsNoTracking()
            .Where(entity => entity.Id == parcelId)
            .Select(entity => new { entity.CadastralNumber })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Land parcel '{parcelId}' was not found.");

        var completedAt = DateTimeOffset.UtcNow;
        var failures = new List<LandParcelGisEnrichmentSectionFailure>();

        var administrative = await InvokeSectionAsync(
            "Administrative",
            failures,
            () => _administrativeVerificationService.VerifyAsync(parcelId, cancellationToken));

        var road = await InvokeSectionAsync(
            "RoadAccessibility",
            failures,
            () => _roadAccessibilityEnrichmentService.EnrichAsync(parcelId, cancellationToken));

        var water = await InvokeSectionAsync(
            "WaterProximity",
            failures,
            () => _waterProximityEnrichmentService.EnrichAsync(parcelId, cancellationToken));

        var soil = await InvokeSectionAsync(
            "Soil",
            failures,
            () => _soilGroupEnrichmentService.EnrichAsync(parcelId, cancellationToken));

        var environmental = await InvokeSectionAsync(
            "Environmental",
            failures,
            () => _environmentalEnrichmentService.EnrichAsync(parcelId, cancellationToken));

        var warnings = LandParcelGisEnrichmentStatusEvaluator.BuildWarnings(
            administrative,
            road,
            water,
            soil,
            environmental,
            failures);

        var evidence = LandParcelGisEnrichmentEvidenceAggregator.Aggregate(
            administrative,
            road,
            water,
            soil,
            environmental);

        var geometryBasis = LandParcelGisEnrichmentStatusEvaluator.ResolveGeometryBasis(
            administrative,
            road,
            water,
            soil,
            environmental);

        var overallStatus = LandParcelGisEnrichmentStatusEvaluator.DetermineOverallStatus(
            administrative,
            road,
            water,
            soil,
            environmental,
            failures);

        return new LandParcelGisEnrichmentResult
        {
            ParcelId = parcelId,
            CadastralNumber = parcelIdentity.CadastralNumber,
            OverallStatus = overallStatus,
            GeometryBasis = geometryBasis,
            Administrative = administrative,
            RoadAccessibility = road,
            WaterProximity = water,
            Soil = soil,
            Environmental = environmental,
            Evidence = evidence,
            Warnings = warnings,
            Failures = failures,
            CompletedAt = completedAt
        };
    }

    private async Task<T?> InvokeSectionAsync<T>(
        string section,
        ICollection<LandParcelGisEnrichmentSectionFailure> failures,
        Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "GIS enrichment section '{Section}' failed for parcel enrichment orchestration.",
                section);

            failures.Add(new LandParcelGisEnrichmentSectionFailure
            {
                Section = section,
                Message = ex.Message
            });

            return default;
        }
    }
}
