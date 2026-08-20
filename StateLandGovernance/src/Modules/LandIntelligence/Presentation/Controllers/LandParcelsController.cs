using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Queries;
using StateLandGovernance.LandIntelligence.Presentation.Mappings;
using StateLandGovernance.LandIntelligence.Presentation.Models;

namespace StateLandGovernance.LandIntelligence.Presentation.Controllers;

[ApiController]
[Route("api/v1/land/parcels")]
[Produces("application/json")]
public sealed class LandParcelsController : ControllerBase
{
    private readonly SearchLandParcelsQueryHandler _searchHandler;
    private readonly GetLandParcelByIdQueryHandler _getByIdHandler;
    private readonly GetSpatialConstraintsByParcelIdQueryHandler _constraintsHandler;
    private readonly GetLandRelationshipsQueryHandler _relationshipsHandler;
    private readonly CreateLandParcelCommandHandler _createHandler;
    private readonly UpdateLandParcelCommandHandler _updateHandler;

    public LandParcelsController(
        SearchLandParcelsQueryHandler searchHandler,
        GetLandParcelByIdQueryHandler getByIdHandler,
        GetSpatialConstraintsByParcelIdQueryHandler constraintsHandler,
        GetLandRelationshipsQueryHandler relationshipsHandler,
        CreateLandParcelCommandHandler createHandler,
        UpdateLandParcelCommandHandler updateHandler)
    {
        _searchHandler = searchHandler;
        _getByIdHandler = getByIdHandler;
        _constraintsHandler = constraintsHandler;
        _relationshipsHandler = relationshipsHandler;
        _createHandler = createHandler;
        _updateHandler = updateHandler;
    }

    /// <summary>
    /// Lists land parcels using optional filter query parameters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(LandSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LandSearchResponse>> ListParcelsAsync(
        [FromQuery] LandSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _searchHandler.HandleAsync(new SearchLandParcelsQuery(request), cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Creates a new land parcel.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(LandParcelDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LandParcelDto>> CreateParcelAsync(
        [FromBody] CreateLandParcelRequest request,
        CancellationToken cancellationToken = default)
    {
        var parcel = await _createHandler.HandleAsync(
            LandParcelRequestMapper.ToCreateCommand(request),
            cancellationToken);

        return Created($"/api/v1/land/parcels/{parcel.Id}", parcel);
    }

    /// <summary>
    /// Updates an existing land parcel.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LandParcelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LandParcelDto>> UpdateParcelAsync(
        Guid id,
        [FromBody] UpdateLandParcelRequest request,
        CancellationToken cancellationToken = default)
    {
        var parcel = await _updateHandler.HandleAsync(
            LandParcelRequestMapper.ToUpdateCommand(id, request),
            cancellationToken);

        return Ok(parcel);
    }

    /// <summary>
    /// Gets a land parcel by identifier.
    /// </summary>
    [HttpGet("{id:guid}", Name = LandParcelRoutes.GetById)]
    [ProducesResponseType(typeof(LandParcelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LandParcelDto>> GetParcelByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var parcel = await _getByIdHandler.HandleAsync(new GetLandParcelByIdQuery(id), cancellationToken);
        return Ok(parcel);
    }

    /// <summary>
    /// Gets spatial constraints for a land parcel.
    /// </summary>
    [HttpGet("{id:guid}/constraints")]
    [ProducesResponseType(typeof(IReadOnlyList<SpatialConstraintDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SpatialConstraintDto>>> GetParcelConstraintsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var constraints = await _constraintsHandler.HandleAsync(
            new GetSpatialConstraintsByParcelIdQuery(id),
            cancellationToken);

        return Ok(constraints);
    }

    /// <summary>
    /// Gets knowledge graph relationships for a land parcel.
    /// </summary>
    [HttpGet("{id:guid}/relationships")]
    [ProducesResponseType(typeof(LandRelationshipsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<LandRelationshipsResponse>> GetParcelRelationshipsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var relationships = await _relationshipsHandler.HandleAsync(
            new GetLandRelationshipsQuery(id),
            cancellationToken);

        return Ok(relationships);
    }
}
