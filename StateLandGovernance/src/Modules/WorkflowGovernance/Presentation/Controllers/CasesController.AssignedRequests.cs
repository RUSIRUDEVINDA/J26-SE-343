namespace StateLandGovernance.WorkflowGovernance.Presentation.Controllers;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
public sealed partial class CasesController
{
    [HttpGet("assigned-institutional-requests")]
    public async Task<IActionResult> AssignedInstitutionalRequests([FromServices] WorkflowExecutionService service, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("authority_case_id")?.Value, out var caseId) || InstitutionActor(caseId) is not {} actor)
            return StatusCode(403);
        var state=await service.GetAsync(caseId,ct);
        var own=state.Stages.Where(s=>s.Institution==actor.Institution);
        return Ok(new { caseId, institution=actor.Institution, executionStatus=state.Status,
            requests=own.Select(s=>new { stageId=s.Id, s.Code, s.Status,
                href=$"/workflow-governance/institutional/{caseId:D}/{s.Id:D}" }) });
    }
}
