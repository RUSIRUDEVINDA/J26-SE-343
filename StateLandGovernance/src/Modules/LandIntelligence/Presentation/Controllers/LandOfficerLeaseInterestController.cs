using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.LandIntelligence.Application.Security;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;
using StateLandGovernance.LandIntelligence.Presentation.Models;
using StateLandGovernance.LandIntelligence.Presentation.Models.Responses;
using ValidationException = StateLandGovernance.LandIntelligence.Application.Interfaces.ValidationException;

namespace StateLandGovernance.LandIntelligence.Presentation.Controllers;

/// <summary>
/// Officer NIC lookup for lease-interest requests (research demo only — not production identity).
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = LandIntelligenceApiGroups.External)]
[Route("api/v1/land/officer/lease-interest")]
[Authorize(Policy = LandIntelligenceAuth.Policies.Officer)]
[Produces("application/json")]
public sealed class LandOfficerLeaseInterestController : ControllerBase
{
    private readonly ILeaseInterestOfficerLookupService _lookup;
    private readonly ILeaseInterestOfficerReviewService _review;
    private readonly ILeaseInterestValuationService _valuations;
    private readonly ILeaseInterestOfficerAccessGate _accessGate;

    public LandOfficerLeaseInterestController(
        ILeaseInterestOfficerLookupService lookup,
        ILeaseInterestOfficerReviewService review,
        ILeaseInterestValuationService valuations,
        ILeaseInterestOfficerAccessGate accessGate)
    {
        _lookup = lookup;
        _review = review;
        _valuations = valuations;
        _accessGate = accessGate;
    }

    [HttpGet("requests/{requestId:guid}/valuations")]
    [ProducesResponseType(typeof(IReadOnlyList<LeaseInterestValuationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LeaseInterestValuationDto>>> ListValuationsAsync(
        Guid requestId, CancellationToken cancellationToken = default)
    {
        if (!_accessGate.IsPreliminaryReviewEnabled) return Forbid();
        return Ok(await _valuations.ListAsync(requestId, cancellationToken));
    }

    [HttpPost("requests/{requestId:guid}/valuations")]
    [ProducesResponseType(typeof(LeaseInterestValuationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LeaseInterestValuationDto>> RecordValuationAsync(
        Guid requestId,
        [FromBody] RecordLeaseInterestValuationDto? input,
        CancellationToken cancellationToken = default)
    {
        if (!_accessGate.IsPreliminaryReviewEnabled) return Forbid();
        if (input is null)
        {
            return BadRequest(new ApiErrorResponse(
                StatusCodes.Status400BadRequest,
                "Validation failed",
                "The valuation request body is required.",
                ["Provide the valuation worksheet fields in the request body."]));
        }

        var idempotencyKey = Request.Headers.TryGetValue("Idempotency-Key", out var values)
            ? values.FirstOrDefault()
            : null;

        try
        {
            return Ok(await _valuations.RecordAsync(requestId, input, idempotencyKey, cancellationToken));
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ApiErrorResponse(
                StatusCodes.Status400BadRequest,
                "Validation failed",
                "The valuation could not be recorded.",
                ex.Errors.ToList()));
        }
        catch (LeaseInterestInvalidStateException ex)
        {
            var isConcurrency = ex.Message.Contains("same time", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("changed while the valuation", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("Refresh and try again", StringComparison.OrdinalIgnoreCase);
            return StatusCode(
                isConcurrency ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest,
                new ApiErrorResponse(
                    isConcurrency ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest,
                    isConcurrency ? "Conflict" : "Invalid request state",
                    ex.Message,
                    [ex.Message]));
        }
    }

    [HttpPost("lookup")]
    [ProducesResponseType(typeof(LeaseInterestOfficerLookupResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LeaseInterestOfficerLookupResponseDto>> LookupByNicAsync(
        [FromBody] LeaseInterestOfficerLookupRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!_accessGate.IsNicLookupEnabled)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new ApiErrorResponse(
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    "Officer NIC lookup is not available in this environment."));
        }

        try
        {
            var result = await _lookup.LookupByNicAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(
                new ApiErrorResponse(
                    StatusCodes.Status400BadRequest,
                    "Validation failed",
                    "The lookup request could not be processed.",
                    ex.Errors.ToList()));
        }
    }

    [HttpGet("queue")]
    [ProducesResponseType(typeof(LeaseInterestOfficerQueueResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LeaseInterestOfficerQueueResponseDto>> ListReviewQueueAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!_accessGate.IsPreliminaryReviewEnabled)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new ApiErrorResponse(
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    "Officer preliminary review is not available in this environment."));
        }

        var result = await _review.ListQueueAsync(
            new LeaseInterestOfficerQueueRequestDto(page, pageSize),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("valuation-queue")]
    [ProducesResponseType(typeof(LeaseInterestOfficerQueueResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LeaseInterestOfficerQueueResponseDto>> ListValuationQueueAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!_accessGate.IsPreliminaryReviewEnabled)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new ApiErrorResponse(
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    "Officer preliminary review is not available in this environment."));
        }

        var result = await _review.ListValuationQueueAsync(
            new LeaseInterestOfficerQueueRequestDto(page, pageSize),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("requests/{requestId:guid}")]
    [ProducesResponseType(typeof(LeaseInterestOfficerRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaseInterestOfficerRequestDetailDto>> GetForReviewAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (!_accessGate.IsPreliminaryReviewEnabled)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new ApiErrorResponse(
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    "Officer preliminary review is not available in this environment."));
        }

        var detail = await _review.GetForReviewAsync(requestId, cancellationToken);
        return Ok(detail);
    }

    [HttpPost("requests/{requestId:guid}/status")]
    [ProducesResponseType(typeof(LeaseInterestOfficerRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LeaseInterestOfficerRequestDetailDto>> UpdateStatusAsync(
        Guid requestId,
        [FromBody] LeaseInterestOfficerStatusUpdateDto update,
        CancellationToken cancellationToken = default)
    {
        if (!_accessGate.IsPreliminaryReviewEnabled)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new ApiErrorResponse(
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    "Officer preliminary review is not available in this environment."));
        }

        try
        {
            var detail = await _review.UpdateStatusAsync(requestId, update, cancellationToken);
            return Ok(detail);
        }
        catch (ValidationException ex)
        {
            return BadRequest(
                new ApiErrorResponse(
                    StatusCodes.Status400BadRequest,
                    "Validation failed",
                    "The status update could not be processed.",
                    ex.Errors.ToList()));
        }
    }
}
