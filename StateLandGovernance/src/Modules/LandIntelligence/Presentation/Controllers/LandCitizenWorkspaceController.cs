using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.LandIntelligence.Application.Security;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Presentation.Models;
using StateLandGovernance.LandIntelligence.Presentation.Models.Responses;

namespace StateLandGovernance.LandIntelligence.Presentation.Controllers;

/// <summary>
/// Owner-scoped shortlist and lease interest requests (preliminary assessment only).
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = LandIntelligenceApiGroups.External)]
[Route("api/v1/land/me")]
[Authorize(Policy = LandIntelligenceAuth.Policies.Applicant)]
[Produces("application/json")]
public sealed class LandCitizenWorkspaceController : ControllerBase
{
    private readonly IParcelShortlistService _shortlist;
    private readonly ILeaseInterestRequestService _leaseInterest;

    public LandCitizenWorkspaceController(
        IParcelShortlistService shortlist,
        ILeaseInterestRequestService leaseInterest)
    {
        _shortlist = shortlist;
        _leaseInterest = leaseInterest;
    }

    [HttpGet("shortlist")]
    [ProducesResponseType(typeof(IReadOnlyList<ParcelShortlistItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ParcelShortlistItemDto>>> ListShortlistAsync(
        CancellationToken cancellationToken)
    {
        var items = await _shortlist.ListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpPut("shortlist/{landParcelId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveShortlistItemAsync(
        Guid landParcelId,
        CancellationToken cancellationToken)
    {
        await _shortlist.SaveAsync(landParcelId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("shortlist/{landParcelId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveShortlistItemAsync(
        Guid landParcelId,
        CancellationToken cancellationToken)
    {
        await _shortlist.RemoveAsync(landParcelId, cancellationToken);
        return NoContent();
    }

    [HttpGet("lease-interest/document-checklist")]
    [ProducesResponseType(typeof(LeaseInterestDocumentChecklistDto), StatusCodes.Status200OK)]
    public ActionResult<LeaseInterestDocumentChecklistDto> GetDocumentChecklist()
    {
        return Ok(_leaseInterest.GetDocumentChecklist());
    }

    [HttpGet("lease-interest/requests")]
    [ProducesResponseType(typeof(IReadOnlyList<LeaseInterestRequestSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LeaseInterestRequestSummaryDto>>> ListMyRequestsAsync(
        CancellationToken cancellationToken)
    {
        var items = await _leaseInterest.ListMineAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("lease-interest/requests/{requestId:guid}")]
    [ProducesResponseType(typeof(LeaseInterestRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaseInterestRequestDetailDto>> GetRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var detail = await _leaseInterest.GetByIdAsync(requestId, cancellationToken);
        return Ok(detail);
    }

    [HttpPost("parcels/{landParcelId:guid}/lease-interest/draft")]
    [ProducesResponseType(typeof(LeaseInterestRequestDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeaseInterestRequestDetailDto>> GetOrCreateDraftAsync(
        Guid landParcelId,
        CancellationToken cancellationToken)
    {
        var detail = await _leaseInterest.GetDraftOrCreateAsync(landParcelId, cancellationToken);
        return Ok(detail);
    }

    [HttpPut("lease-interest/requests/{requestId:guid}/draft")]
    [ProducesResponseType(typeof(LeaseInterestRequestDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeaseInterestRequestDetailDto>> SaveDraftAsync(
        Guid requestId,
        [FromBody] UpsertLeaseInterestDraftDto draft,
        CancellationToken cancellationToken)
    {
        var detail = await _leaseInterest.SaveDraftAsync(requestId, draft, cancellationToken);
        return Ok(detail);
    }

    [HttpPost("lease-interest/requests/{requestId:guid}/submit")]
    [ProducesResponseType(typeof(LeaseInterestRequestDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeaseInterestRequestDetailDto>> SubmitAsync(
        Guid requestId,
        [FromBody] UpsertLeaseInterestDraftDto draft,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = Request.Headers.TryGetValue("Idempotency-Key", out var values)
            ? values.FirstOrDefault()
            : null;

        var detail = await _leaseInterest.SubmitAsync(
            requestId,
            draft,
            idempotencyKey,
            cancellationToken);
        return Ok(detail);
    }
}
