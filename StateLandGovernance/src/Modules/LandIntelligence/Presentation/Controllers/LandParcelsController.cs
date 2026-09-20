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

    public LandParcelsController(
        SearchLandParcelsQueryHandler searchHandler,
        GetLandParcelByIdQueryHandler getByIdHandler,
        GetSpatialConstraintsByParcelIdQueryHandler constraintsHandler,
        GetLandRelationshipsQueryHandler relationshipsHandler)
    {
        _searchHandler = searchHandler;
        _getByIdHandler = getByIdHandler;
        _constraintsHandler = constraintsHandler;
        _relationshipsHandler = relationshipsHandler;
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
