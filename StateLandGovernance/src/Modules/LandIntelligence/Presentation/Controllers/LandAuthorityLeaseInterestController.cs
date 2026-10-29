using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Security;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;
using StateLandGovernance.LandIntelligence.Presentation.Models;
using ValidationException = StateLandGovernance.LandIntelligence.Application.Interfaces.ValidationException;

namespace StateLandGovernance.LandIntelligence.Presentation.Controllers;

/// <summary>
/// Authority review after officer valuation (research demo only — not production RBAC).
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = LandIntelligenceApiGroups.External)]
[Route("api/v1/land/authority/lease-interest")]
[Authorize(Policy = LandIntelligenceAuth.Policies.Authority)]
[Produces("application/json")]
public sealed class LandAuthorityLeaseInterestController : ControllerBase
{
    private readonly ILeaseInterestAuthorityReviewService _review;
    private readonly ILeaseInterestOfficerAccessGate _accessGate;

    public LandAuthorityLeaseInterestController(
        ILeaseInterestAuthorityReviewService review,
        ILeaseInterestOfficerAccessGate accessGate)
    {
        _review = review;
        _accessGate = accessGate;
    }

    [HttpGet("queue")]
    [ProducesResponseType(typeof(LeaseInterestAuthorityQueueResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LeaseInterestAuthorityQueueResponseDto>> ListQueueAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (!_accessGate.IsAuthorityReviewEnabled)
        {
            return ForbiddenReview();
        }

        try
        {
            return Ok(
                await _review.ListQueueAsync(
                    new LeaseInterestAuthorityQueueRequestDto(page, pageSize, statusFilter),
                    cancellationToken));
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ApiErrorResponse(
                StatusCodes.Status400BadRequest,
                "Validation failed",
                "The authority queue could not be listed.",
                ex.Errors.ToList()));
        }
    }

    [HttpGet("requests/{requestId:guid}")]
    [ProducesResponseType(typeof(LeaseInterestAuthorityRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaseInterestAuthorityRequestDetailDto>> GetForReviewAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (!_accessGate.IsAuthorityReviewEnabled)
        {
            return ForbiddenReview();
        }

        return Ok(await _review.GetForReviewAsync(requestId, cancellationToken));
    }

    [HttpPost("requests/{requestId:guid}/decisions")]
    [ProducesResponseType(typeof(LeaseInterestAuthorityRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LeaseInterestAuthorityRequestDetailDto>> RecordDecisionAsync(
        Guid requestId,
        [FromBody] RecordLeaseInterestAuthorityDecisionDto? input,
        CancellationToken cancellationToken = default)
    {
        if (!_accessGate.IsAuthorityReviewEnabled)
        {
            return ForbiddenReview();
        }

        if (input is null)
        {
            return BadRequest(new ApiErrorResponse(
                StatusCodes.Status400BadRequest,
                "Validation failed",
                "The decision body is required.",
                ["Provide decision, reason, expectedUpdatedAtUtc, reviewedValuationId, and reviewedValuationRevision."]));
        }

        try
        {
            return Ok(await _review.RecordDecisionAsync(requestId, input, cancellationToken));
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ApiErrorResponse(
                StatusCodes.Status400BadRequest,
                "Validation failed",
                "The authority decision could not be recorded.",
                ex.Errors.ToList()));
        }
        catch (LeaseInterestInvalidStateException ex)
        {
            var isConcurrency = ex.Message.Contains("updated by someone else", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("already has an authority outcome", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("valuation changed", StringComparison.OrdinalIgnoreCase);
            return StatusCode(
                isConcurrency ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest,
                new ApiErrorResponse(
                    isConcurrency ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest,
                    isConcurrency ? "Conflict" : "Invalid request state",
                    ex.Message,
                    [ex.Message]));
        }
    }

    private ObjectResult ForbiddenReview() =>
        StatusCode(
            StatusCodes.Status403Forbidden,
            new ApiErrorResponse(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "Authority review is not available in this environment."));
}
