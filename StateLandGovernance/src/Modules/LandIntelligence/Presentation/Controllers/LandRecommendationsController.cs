using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Queries;
using StateLandGovernance.LandIntelligence.Presentation.Models;

namespace StateLandGovernance.LandIntelligence.Presentation.Controllers;

[ApiController]
[Route("api/v1/land/recommendations")]
[Produces("application/json")]
public sealed class LandRecommendationsController : ControllerBase
{
    private readonly SearchLandRecommendationsQueryHandler _searchRecommendationsHandler;
    private readonly GetLandRecommendationByIdQueryHandler _getRecommendationByIdHandler;

    public LandRecommendationsController(
        SearchLandRecommendationsQueryHandler searchRecommendationsHandler,
        GetLandRecommendationByIdQueryHandler getRecommendationByIdHandler)
    {
        _searchRecommendationsHandler = searchRecommendationsHandler;
        _getRecommendationByIdHandler = getRecommendationByIdHandler;
    }

    /// <summary>
    /// Generates explainable land recommendations for candidate parcels.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(LandRecommendationSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<LandRecommendationSearchResponse>> CreateRecommendationsAsync(
        [FromBody] LandRecommendationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _searchRecommendationsHandler.HandleAsync(
            new SearchLandRecommendationsQuery(request),
            cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Gets a persisted land recommendation by identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LandRecommendationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LandRecommendationDto>> GetRecommendationByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var recommendation = await _getRecommendationByIdHandler.HandleAsync(
            new GetLandRecommendationByIdQuery(id),
            cancellationToken);

        return Ok(recommendation);
    }
}
