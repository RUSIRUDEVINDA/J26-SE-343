namespace StateLandGovernance.WorkflowGovernance.Presentation.Controllers;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

public sealed partial class CasesController
{
    private VerifiedAuthorityContext? PlanningAuthority(Guid caseId)
    {
        // Capabilities must originate in authenticated claims, never browser request bodies or the Officer role alone.
        if (User.Identity?.IsAuthenticated != true) return null;
        var capabilities = User.FindAll("capability").Select(c => c.Value).Distinct().ToList();
        if (!capabilities.Contains("WorkflowPlanner")) return null;
        var scope = User.FindFirst("authority_case_id")?.Value;
        if (scope != caseId.ToString("D")) return null;
        var expires = User.FindFirst("exp")?.Value;
        if (!long.TryParse(expires, out var expiry)) return null;
        var now = DateTime.UtcNow;
        if (expiry <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() || expiry > 253402300799) return null;
        return new(ResolveActorId(), capabilities, "LeaseCase", scope, now.AddSeconds(-1), now, DateTimeOffset.FromUnixTimeSeconds(expiry).UtcDateTime);
    }
    [HttpGet("{caseId:guid}/workflow-plan")]
    public async Task<IActionResult> GetWorkflowPlan(Guid caseId, [FromServices] WorkflowPlanningService planning, CancellationToken ct)
    {
        if (!IsOfficer()) return StatusCode(403);
        return Ok(await planning.GetAsync(caseId, PlanningAuthority(caseId), ct));
    }
    [HttpPost("{caseId:guid}/workflow-plan/generate")]
    public async Task<IActionResult> GenerateWorkflowPlan(Guid caseId, [FromBody] GenerateWorkflowPlanRequest request,
        [FromServices] WorkflowPlanningService planning, CancellationToken ct)
    {
        if (!IsOfficer() || PlanningAuthority(caseId) is not { } authority) return StatusCode(403);
        return Ok(await planning.GenerateAsync(caseId, request, authority, ct));
    }
    [HttpPost("{caseId:guid}/workflow-plan/{planId:guid}/approve")]
    public async Task<IActionResult> ApproveWorkflowPlan(Guid caseId, Guid planId, [FromBody] ApproveWorkflowPlanRequest request,
        [FromServices] WorkflowPlanningService planning, CancellationToken ct)
    {
        if (!IsOfficer() || PlanningAuthority(caseId) is not { } authority || !authority.Capabilities.Contains("WorkflowPlanApprover")) return StatusCode(403);
        return Ok(await planning.ApproveAsync(caseId, planId, request, authority, ct));
    }
}
