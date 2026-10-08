using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Queries;
using StateLandGovernance.LandIntelligence.Presentation.Mappings;
using StateLandGovernance.LandIntelligence.Presentation.Models;
using StateLandGovernance.LandIntelligence.Presentation.Models.Responses;

namespace StateLandGovernance.LandIntelligence.Presentation.Controllers;

/// <summary>
/// Read-only recommendation query endpoints for external platform modules.
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = LandIntelligenceApiGroups.External)]
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
    /// Evaluates explainable land recommendations for candidate parcels without mutating parcel data.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(LandRecommendationSearchResultsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<LandRecommendationSearchResultsResponse>> EvaluateRecommendationsAsync(
        [FromBody] LandRecommendationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _searchRecommendationsHandler.HandleAsync(
            new SearchLandRecommendationsQuery(request),
            cancellationToken);

        return Ok(LandIntelligenceApiResponseMapper.ToResponse(response));
    }

    /// <summary>
    /// Gets a persisted land recommendation by identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LandRecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LandRecommendationResponse>> GetRecommendationByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var recommendation = await _getRecommendationByIdHandler.HandleAsync(
            new GetLandRecommendationByIdQuery(id),
            cancellationToken);

        return Ok(LandIntelligenceApiResponseMapper.ToResponse(recommendation));
    }
}
