using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Queries;
using StateLandGovernance.LandIntelligence.Presentation.Mappings;
using StateLandGovernance.LandIntelligence.Presentation.Models;
using StateLandGovernance.LandIntelligence.Presentation.Models.Responses;

namespace StateLandGovernance.LandIntelligence.Presentation.Controllers;

/// <summary>
/// Read-only land parcel endpoints for external platform modules.
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = LandIntelligenceApiGroups.External)]
[Route("api/v1/land/parcels")]
[Produces("application/json")]
public sealed class LandParcelsController : ControllerBase
{
    private readonly SearchLandParcelsQueryHandler _searchHandler;
    private readonly GetLandParcelByIdQueryHandler _getByIdHandler;
    private readonly GetSpatialConstraintsByParcelIdQueryHandler _constraintsHandler;
    private readonly GetLandRelationshipsQueryHandler _relationshipsHandler;
    private readonly ListSourceReportedLandUseOptionsQueryHandler _sourceLandUseOptionsHandler;
    private readonly ListDerivedSoilGroupOptionsQueryHandler _derivedSoilGroupOptionsHandler;
    private readonly GetLandParcelMetricsQueryHandler _metricsHandler;
    private readonly GetLandIntelligenceGisCoverageQueryHandler _gisCoverageHandler;
    private readonly ListGramaNiladhariDivisionOptionsQueryHandler _gramaNiladhariDivisionOptionsHandler;
    private readonly ListApprovedPreliminaryParcelsQueryHandler _approvedPreliminaryParcelsHandler;

    public LandParcelsController(
        SearchLandParcelsQueryHandler searchHandler,
        GetLandParcelByIdQueryHandler getByIdHandler,
        GetSpatialConstraintsByParcelIdQueryHandler constraintsHandler,
        GetLandRelationshipsQueryHandler relationshipsHandler,
        ListSourceReportedLandUseOptionsQueryHandler sourceLandUseOptionsHandler,
        ListDerivedSoilGroupOptionsQueryHandler derivedSoilGroupOptionsHandler,
        GetLandParcelMetricsQueryHandler metricsHandler,
        GetLandIntelligenceGisCoverageQueryHandler gisCoverageHandler,
        ListGramaNiladhariDivisionOptionsQueryHandler gramaNiladhariDivisionOptionsHandler,
        ListApprovedPreliminaryParcelsQueryHandler approvedPreliminaryParcelsHandler)
    {
        _searchHandler = searchHandler;
        _getByIdHandler = getByIdHandler;
        _constraintsHandler = constraintsHandler;
        _relationshipsHandler = relationshipsHandler;
        _sourceLandUseOptionsHandler = sourceLandUseOptionsHandler;
        _derivedSoilGroupOptionsHandler = derivedSoilGroupOptionsHandler;
        _metricsHandler = metricsHandler;
        _gisCoverageHandler = gisCoverageHandler;
        _gramaNiladhariDivisionOptionsHandler = gramaNiladhariDivisionOptionsHandler;
        _approvedPreliminaryParcelsHandler = approvedPreliminaryParcelsHandler;
    }

    [HttpGet("metrics")]
    [ProducesResponseType(typeof(LandParcelMetricsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LandParcelMetricsResponse>> GetParcelMetricsAsync(
        CancellationToken cancellationToken = default)
    {
        var metrics = await _metricsHandler.HandleAsync(new GetLandParcelMetricsQuery(), cancellationToken);
        return Ok(LandIntelligenceApiResponseMapper.ToResponse(metrics));
    }

    [HttpGet("gis-coverage")]
    [ProducesResponseType(typeof(LandIntelligenceGisCoverageResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LandIntelligenceGisCoverageResponse>> GetGisCoverageAsync(
        CancellationToken cancellationToken = default)
    {
        var coverage = await _gisCoverageHandler.HandleAsync(
            new GetLandIntelligenceGisCoverageQuery(),
            cancellationToken);
        return Ok(LandIntelligenceApiResponseMapper.ToResponse(coverage));
    }

    /// <summary>
    /// Lists land parcels using optional filter query parameters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(LandSearchResultsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LandSearchResultsResponse>> ListParcelsAsync(
        [FromQuery] LandSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _searchHandler.HandleAsync(new SearchLandParcelsQuery(request), cancellationToken);
        return Ok(LandIntelligenceApiResponseMapper.ToResponse(response));
    }

    /// <summary>
    /// Lists distinct source-reported land-use codes present in imported cadastre metadata.
    /// </summary>
    [HttpGet("source-reported-land-use-options")]
    [ProducesResponseType(typeof(IReadOnlyList<SourceReportedLandUseOptionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SourceReportedLandUseOptionResponse>>> ListSourceReportedLandUseOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var options = await _sourceLandUseOptionsHandler.HandleAsync(
            new ListSourceReportedLandUseOptionsQuery(),
            cancellationToken);

        return Ok(options
            .Select(option => new SourceReportedLandUseOptionResponse(option.Code, option.EnglishLabel, option.ParcelCount))
            .ToList());
    }

    /// <summary>
    /// Lists distinct GIS-derived soil group names for parcels in an optional district.
    /// Empty when the district has no imported soil coverage.
    /// </summary>
    [HttpGet("derived-soil-group-options")]
    [ProducesResponseType(typeof(DerivedSoilGroupOptionsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DerivedSoilGroupOptionsResponse>> ListDerivedSoilGroupOptionsAsync(
        [FromQuery] string? district = null,
        CancellationToken cancellationToken = default)
    {
        var options = await _derivedSoilGroupOptionsHandler.HandleAsync(
            new ListDerivedSoilGroupOptionsQuery(district),
            cancellationToken);
        return Ok(new DerivedSoilGroupOptionsResponse(options));
    }

    /// <summary>
    /// Lists distinct Grama Niladhari division names that have at least one imported land parcel (optional district filter).
    /// </summary>
    [HttpGet("grama-niladhari-division-options")]
    [ProducesResponseType(typeof(GramaNiladhariDivisionOptionsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GramaNiladhariDivisionOptionsResponse>> ListGramaNiladhariDivisionOptionsAsync(
        [FromQuery] string? district = null,
        CancellationToken cancellationToken = default)
    {
        var options = await _gramaNiladhariDivisionOptionsHandler.HandleAsync(
            new ListGramaNiladhariDivisionOptionsQuery(district),
            cancellationToken);
        return Ok(new GramaNiladhariDivisionOptionsResponse(options));
    }

    /// <summary>
    /// Lists parcels with a current authority-approved preliminary lease interest request (ApprovedInPrinciple).
    /// Not an executed lease or land reservation.
    /// </summary>
    [HttpGet("approved-preliminary")]
    [ProducesResponseType(typeof(ApprovedPreliminaryParcelListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApprovedPreliminaryParcelListResponse>> ListApprovedPreliminaryParcelsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var response = await _approvedPreliminaryParcelsHandler.HandleAsync(
            new ListApprovedPreliminaryParcelsQuery(page, pageSize),
            cancellationToken);
        return Ok(LandIntelligenceApiResponseMapper.ToApprovedPreliminaryParcelListResponse(response));
    }

    /// <summary>
    /// Gets a land parcel by identifier.
    /// </summary>
    [HttpGet("{id:guid}", Name = LandParcelRoutes.GetById)]
    [ProducesResponseType(typeof(LandParcelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LandParcelResponse>> GetParcelByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var parcel = await _getByIdHandler.HandleAsync(new GetLandParcelByIdQuery(id), cancellationToken);
        return Ok(LandIntelligenceApiResponseMapper.ToResponse(parcel));
    }

    /// <summary>
    /// Gets spatial constraints for a land parcel.
    /// </summary>
    [HttpGet("{id:guid}/constraints")]
    [ProducesResponseType(typeof(IReadOnlyList<SpatialConstraintResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SpatialConstraintResponse>>> GetParcelConstraintsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var constraints = await _constraintsHandler.HandleAsync(
            new GetSpatialConstraintsByParcelIdQuery(id),
            cancellationToken);

        return Ok(constraints.Select(LandIntelligenceApiResponseMapper.ToResponse).ToList());
    }

    /// <summary>
    /// Gets knowledge graph relationships for a land parcel.
    /// </summary>
    [HttpGet("{id:guid}/relationships")]
    [ProducesResponseType(typeof(Models.Responses.LandRelationshipsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<Models.Responses.LandRelationshipsResponse>> GetParcelRelationshipsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var relationships = await _relationshipsHandler.HandleAsync(
            new GetLandRelationshipsQuery(id),
            cancellationToken);

        return Ok(LandIntelligenceApiResponseMapper.ToResponse(relationships));
    }
}
