using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Queries;
using StateLandGovernance.LandIntelligence.Presentation.Models;

namespace StateLandGovernance.LandIntelligence.Presentation.Controllers;

[ApiController]
[Route("api/v1/land")]
[Produces("application/json")]
public sealed class LandSearchController : ControllerBase
{
    private readonly SearchLandParcelsQueryHandler _searchHandler;

    public LandSearchController(SearchLandParcelsQueryHandler searchHandler)
    {
        _searchHandler = searchHandler;
    }

    /// <summary>
    /// Searches land parcels using structured filter criteria in the request body.
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(typeof(LandSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LandSearchResponse>> SearchParcelsAsync(
        [FromBody] LandSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _searchHandler.HandleAsync(new SearchLandParcelsQuery(request), cancellationToken);
        return Ok(response);
    }
}
