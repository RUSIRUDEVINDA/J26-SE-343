using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Presentation.Mappings;
using StateLandGovernance.LandIntelligence.Presentation.Models;
using StateLandGovernance.LandIntelligence.Presentation.Models.Responses;

namespace StateLandGovernance.LandIntelligence.Presentation.Controllers;

/// <summary>
/// Internal Component 1 parcel persistence endpoints. Not part of the external read-only contract.
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = LandIntelligenceApiGroups.Internal)]
[Route("api/v1/internal/land/parcels")]
[Produces("application/json")]
public sealed class LandParcelsInternalController : ControllerBase
{
    private readonly CreateLandParcelCommandHandler _createHandler;
    private readonly UpdateLandParcelCommandHandler _updateHandler;
    private readonly DeleteLandParcelCommandHandler _deleteHandler;

    public LandParcelsInternalController(
        CreateLandParcelCommandHandler createHandler,
        UpdateLandParcelCommandHandler updateHandler,
        DeleteLandParcelCommandHandler deleteHandler)
    {
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
    }

    [HttpPost]
    [ProducesResponseType(typeof(LandParcelResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LandParcelResponse>> CreateParcelAsync(
        [FromBody] CreateLandParcelRequest request,
        CancellationToken cancellationToken = default)
    {
        var parcel = await _createHandler.HandleAsync(
            LandParcelRequestMapper.ToCreateCommand(request),
            cancellationToken);

        return Created($"/api/v1/land/parcels/{parcel.Id}", LandIntelligenceApiResponseMapper.ToResponse(parcel));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LandParcelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LandParcelResponse>> UpdateParcelAsync(
        Guid id,
        [FromBody] UpdateLandParcelRequest request,
        CancellationToken cancellationToken = default)
    {
        var parcel = await _updateHandler.HandleAsync(
            LandParcelRequestMapper.ToUpdateCommand(id, request),
            cancellationToken);

        return Ok(LandIntelligenceApiResponseMapper.ToResponse(parcel));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteParcelAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await _deleteHandler.HandleAsync(new DeleteLandParcelCommand(id), cancellationToken);
        return NoContent();
    }
}
