using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.LandIntelligence.Application.Security;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
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
    private readonly ILeaseInterestOfficerAccessGate _accessGate;

    public LandOfficerLeaseInterestController(
        ILeaseInterestOfficerLookupService lookup,
        ILeaseInterestOfficerReviewService review,
        ILeaseInterestOfficerAccessGate accessGate)
    {
        _lookup = lookup;
        _review = review;
        _accessGate = accessGate;
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
                    "The NIC lookup request could not be processed.",
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
