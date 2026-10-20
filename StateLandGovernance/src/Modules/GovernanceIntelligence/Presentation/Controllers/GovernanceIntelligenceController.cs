using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.Queries;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Contracts;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Fixtures;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.Timing;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;

namespace StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;

/// <summary>
/// API Controller boundary for the GovernanceIntelligence module.
/// </summary>
[ApiController]
[Route("api/governance-intelligence")]
[ApiExplorerSettings(GroupName = "governance-intelligence")]
public class GovernanceIntelligenceController : ControllerBase
{
    private readonly EvaluateComplianceCommandHandler _complianceHandler;
    private readonly DetectConflictsCommandHandler _conflictHandler;
    private readonly EvaluateGovernanceRiskCommandHandler _riskHandler;
    private readonly GenerateGovernanceExplanationCommandHandler _explanationHandler;
    private readonly EvaluateGovernanceConsensusCommandHandler _consensusHandler;
    private readonly EvaluateConditionalVerificationCommandHandler _verificationHandler;
    private readonly GetGovernanceAuditQueryHandler? _auditQueryHandler;

    public GovernanceIntelligenceController(
        EvaluateComplianceCommandHandler complianceHandler,
        DetectConflictsCommandHandler conflictHandler,
        EvaluateGovernanceRiskCommandHandler riskHandler,
        GenerateGovernanceExplanationCommandHandler explanationHandler,
        EvaluateGovernanceConsensusCommandHandler consensusHandler,
        EvaluateConditionalVerificationCommandHandler verificationHandler,
        GetGovernanceAuditQueryHandler? auditQueryHandler = null)
    {
        _complianceHandler = complianceHandler;
        _conflictHandler = conflictHandler;
        _riskHandler = riskHandler;
        _explanationHandler = explanationHandler;
        _consensusHandler = consensusHandler;
        _verificationHandler = verificationHandler;
        _auditQueryHandler = auditQueryHandler;
    }

    /// <summary>
    /// Evaluates regulatory compliance for a proposed land action.
    /// </summary>
    [HttpPost("evaluate-compliance")]
    [ProducesResponseType(typeof(ComplianceResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ComplianceResultDto>> EvaluateCompliance(
        [FromBody] EvaluateComplianceCommand command,
        CancellationToken cancellationToken)
    {
        if (command is null || string.IsNullOrWhiteSpace(command.ActionName))
        {
            return BadRequest("Invalid command details provided.");
        }

        try
        {
            var result = await _complianceHandler.HandleAsync(command, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Detects jurisdictional or regulatory conflicts across a set of decisions.
    /// </summary>
    [HttpPost("detect-conflicts")]
    [ProducesResponseType(typeof(ConflictDetectionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConflictDetectionResultDto>> DetectConflicts(
        [FromBody] DetectConflictsCommand command,
        CancellationToken cancellationToken)
    {
        if (command is null || string.IsNullOrWhiteSpace(command.ActionName))
        {
            return BadRequest("Invalid command details provided.");
        }

        try
        {
            var result = await _conflictHandler.HandleAsync(command, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Evaluates governance risk observations and produces explainable risk indicators.
    /// </summary>
    [HttpPost("evaluate-risk")]
    [ProducesResponseType(typeof(GovernanceRiskAssessmentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GovernanceRiskAssessmentResultDto>> EvaluateRisk(
        [FromBody] EvaluateGovernanceRiskCommand command,
        CancellationToken cancellationToken)
    {
        if (command is null || string.IsNullOrWhiteSpace(command.ActionName) || command.Input is null)
        {
            return BadRequest("Invalid command or risk input details provided.");
        }

        try
        {
            var result = await _riskHandler.HandleAsync(command, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Synthesizes transparent, explainable governance findings across sub-engine outcomes.
    /// </summary>
    [HttpPost("explain")]
    [ProducesResponseType(typeof(GovernanceExplanationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GovernanceExplanationResultDto>> Explain(
        [FromBody] GenerateGovernanceExplanationCommand command,
        CancellationToken cancellationToken)
    {
        if (command is null || string.IsNullOrWhiteSpace(command.ActionName) || command.Input is null)
        {
            return BadRequest("Invalid command or explanation input details provided.");
        }

        try
        {
            var result = await _explanationHandler.HandleAsync(command, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Evaluates multi-institutional governance consensus across submitted positions and policy.
    /// </summary>
    [HttpPost("evaluate-consensus")]
    [ProducesResponseType(typeof(GovernanceConsensusResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GovernanceConsensusResultDto>> EvaluateConsensus(
        [FromBody] EvaluateGovernanceConsensusCommand command,
        CancellationToken cancellationToken)
    {
        if (command is null || string.IsNullOrWhiteSpace(command.ActionName) || command.Input is null)
        {
            return BadRequest("Invalid command or consensus input details provided.");
        }

        try
        {
            var result = await _consensusHandler.HandleAsync(command, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Evaluates whether required prerequisite governance conditions are satisfied based on supplied evidence.
    /// </summary>
    [HttpPost("verify-conditions")]
    [ProducesResponseType(typeof(ConditionalVerificationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConditionalVerificationResultDto>> VerifyConditions(
        [FromBody] EvaluateConditionalVerificationCommand command,
        CancellationToken cancellationToken)
    {
        if (command is null || string.IsNullOrWhiteSpace(command.ActionName) || command.Input is null)
        {
            return BadRequest("Invalid command or verification input details provided.");
        }

        try
        {
            var result = await _verificationHandler.HandleAsync(command, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Classifies an incoming land lease complaint into the 4-class governance taxonomy.
    /// </summary>
    [HttpPost("classify-complaint")]
    [ProducesResponseType(typeof(ComplaintClassificationAssessment), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ClassifyComplaint(
        [FromBody] ClassifyComplaintCommand command,
        [FromServices] ClassifyComplaintCommandHandler? complaintHandler,
        CancellationToken cancellationToken)
    {
        if (complaintHandler is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Complaint classifier handler is not configured.");
        }

        var result = await complaintHandler.HandleAsync(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error?.Message ?? "Complaint classification failed.");
        }

        return Ok(result.Assessment);
    }

    /// <summary>
    /// Evaluates a completed workflow event trace for execution anomalies using the frozen Isolation Forest model.
    /// </summary>
    [HttpPost("predict-workflow-anomaly")]
    [ProducesResponseType(typeof(WorkflowAnomalyPrediction), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> PredictWorkflowAnomaly(
        [FromBody] WorkflowAnomalyRequestDto request,
        [FromServices] IWorkflowAnomalyClient? anomalyClient,
        CancellationToken cancellationToken)
    {
        if (anomalyClient is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Workflow anomaly client is not configured.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.CaseId) || request.Events is null || request.Events.Count == 0)
        {
            return BadRequest("Invalid workflow anomaly request: caseId and non-empty events collection are required.");
        }

        var result = await anomalyClient.PredictAnomalyAsync(request.CaseId, request.Events, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error?.Message ?? "Workflow anomaly evaluation failed.");
        }

        return Ok(result.Prediction);
    }

    /// <summary>
    /// Performs deterministic early governance screening on applicant/case indicators.
    /// </summary>
    [HttpPost("screen-early-governance")]
    [ProducesResponseType(typeof(EarlyGovernanceScreeningResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ScreenEarlyGovernance(
        [FromBody] ScreenEarlyGovernanceCommand command,
        [FromServices] ScreenEarlyGovernanceCommandHandler? screeningHandler,
        CancellationToken cancellationToken)
    {
        if (screeningHandler is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Early screening handler is not configured.");
        }

        try
        {
            var result = await screeningHandler.HandleAsync(command, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Retrieves the blockchain verification receipt and anchor state for an audit record.
    /// </summary>
    [HttpGet("audits/{auditRecordId:guid}/receipt")]
    [ProducesResponseType(typeof(GovernanceAuditVerificationReceiptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuditReceipt(
        Guid auditRecordId,
        [FromServices] GetAuditReceiptQueryHandler? receiptQueryHandler,
        CancellationToken cancellationToken)
    {
        if (receiptQueryHandler is null)
        {
            return NotFound("Receipt query handler is not configured.");
        }

        var receipt = await receiptQueryHandler.HandleAsync(new GetAuditReceiptQuery(auditRecordId), cancellationToken);
        if (receipt is null)
        {
            return NotFound("Audit record not found.");
        }

        return Ok(receipt);
    }

    [HttpGet("audits/history/cases/{caseId}")]
    public async Task<IActionResult> GetAuditHistoryByCase(
        string caseId, [FromQuery] int limit,
        [FromServices] GetAuditHistoryByCaseQueryHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await handler.HandleAsync(
                new GetAuditHistoryByCaseQuery(caseId, limit == 0 ? 100 : limit), cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    /// <summary>
    /// Idempotently anchors an audit record or reconciles incomplete local receipt metadata.
    /// </summary>
    [HttpPost("audits/{auditRecordId:guid}/anchor")]
    public async Task<IActionResult> AnchorAuditRecord(
        Guid auditRecordId,
        [FromServices] AnchorAuditRecordCommandHandler handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new AnchorAuditRecordCommand(auditRecordId), cancellationToken));

    /// <summary>
    /// Verifies the on-chain ledger integrity for an off-chain audit record against the blockchain service.
    /// </summary>
    [HttpPost("audits/{auditRecordId:guid}/verify")]
    [ProducesResponseType(typeof(GovernanceAuditVerificationReceiptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyAuditIntegrity(
        Guid auditRecordId,
        [FromServices] VerifyAuditIntegrityQueryHandler? verifyIntegrityHandler,
        CancellationToken cancellationToken)
    {
        if (verifyIntegrityHandler is null)
        {
            return NotFound("Verify integrity handler is not configured.");
        }

        var result = await verifyIntegrityHandler.HandleAsync(new VerifyAuditIntegrityQuery(auditRecordId), cancellationToken);
        if (result is null)
        {
            return NotFound("Audit record not found.");
        }

        return Ok(result);
    }

    /// <summary>
    /// Retrieves dashboard summary metrics and historical case evaluations for the Component 4 frontend.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(GovernanceDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetDashboard(
        [FromServices] GetGovernanceDashboardQueryHandler? dashboardHandler,
        CancellationToken cancellationToken)
    {
        if (dashboardHandler is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Dashboard handler is not configured.");
        }

        var result = await dashboardHandler.HandleAsync(new GetGovernanceDashboardQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("workflow-anomalies/assess")]
    public async Task<IActionResult> AssessWorkflowAnomaly(
        [FromBody] AssessWorkflowAnomalyRequestDto request,
        [FromServices] AssessWorkflowAnomalyCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            AssessWorkflowAnomalyCommand.FromProjection(request.WorkflowProjectionId, request.CaseId, request.WorkflowRunId),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Assessment) : BadRequest(result.Error);
    }

    [HttpGet("workflow-anomalies/{assessmentId:guid}")]
    public async Task<IActionResult> GetWorkflowAnomalyAssessment(
        Guid assessmentId,
        [FromServices] GetWorkflowAnomalyAssessmentQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetWorkflowAnomalyAssessmentQuery(assessmentId), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("workflow-anomalies/history/cases/{caseId}")]
    public async Task<IActionResult> GetWorkflowAnomalyHistoryByCase(
        string caseId, [FromQuery] int limit,
        [FromServices] GetWorkflowAnomalyHistoryByCaseQueryHandler handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new GetWorkflowAnomalyHistoryByCaseQuery(caseId, NormalizeLimit(limit)), cancellationToken));

    [HttpGet("workflow-anomalies/history/workflow-runs/{workflowRunId:guid}")]
    public async Task<IActionResult> GetWorkflowAnomalyHistoryByWorkflowRun(
        Guid workflowRunId, [FromQuery] int limit,
        [FromServices] GetWorkflowAnomalyHistoryByWorkflowRunQueryHandler handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new GetWorkflowAnomalyHistoryByWorkflowRunQuery(workflowRunId, NormalizeLimit(limit)), cancellationToken));

    [HttpGet("workflow-projections/cases/{caseId}")]
    [ProducesResponseType(typeof(WorkflowProjectionJourneyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkflowProjectionJourneyByCase(
        string caseId,
        [FromServices] GetWorkflowProjectionJourneyByCaseQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetWorkflowProjectionJourneyByCaseQuery(caseId),
            cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("lease-timing/assess")]
    public async Task<IActionResult> AssessLeaseTiming(
        [FromBody] CalculateLeaseApprovalTimingRequestDto request,
        [FromServices] CalculateLeaseApprovalTimingCommandHandler handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new CalculateLeaseApprovalTimingCommand(request.WorkflowProjectionId, request.AssessmentAtUtc), cancellationToken));

    [HttpGet("lease-timing/{assessmentId:guid}")]
    public async Task<IActionResult> GetLeaseTimingAssessment(
        Guid assessmentId,
        [FromServices] GetLeaseApprovalTimingAssessmentQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetLeaseApprovalTimingAssessmentQuery(assessmentId), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("lease-timing/history/cases/{caseId}")]
    public async Task<IActionResult> GetLeaseTimingHistoryByCase(
        string caseId, [FromQuery] int limit,
        [FromServices] GetLeaseApprovalTimingHistoryByCaseQueryHandler handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new GetLeaseApprovalTimingHistoryByCaseQuery(caseId, NormalizeLimit(limit)), cancellationToken));

    [HttpGet("lease-timing/history/workflow-runs/{workflowRunId:guid}")]
    public async Task<IActionResult> GetLeaseTimingHistoryByWorkflowRun(
        Guid workflowRunId, [FromQuery] int limit,
        [FromServices] GetLeaseApprovalTimingHistoryByWorkflowRunQueryHandler handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new GetLeaseApprovalTimingHistoryByWorkflowRunQuery(workflowRunId, NormalizeLimit(limit)), cancellationToken));

    [HttpGet("screening/{assessmentId:guid}")]
    public async Task<IActionResult> GetScreeningAssessment(
        Guid assessmentId,
        [FromServices] GetEarlyGovernanceScreeningAssessmentQueryHandler handler,
        CancellationToken cancellationToken)
    {
        if (!CanAccessConsolidatedCase(User.FindFirstValue("authority_case_id") ?? "")) return StatusCode(StatusCodes.Status403Forbidden);
        var result = await handler.HandleAsync(new GetEarlyGovernanceScreeningAssessmentQuery(assessmentId), cancellationToken);
        if (result != null && !CanAccessConsolidatedCase(result.Result.CaseId)) return StatusCode(StatusCodes.Status403Forbidden);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("screening/history/cases/{caseId}")]
    public async Task<IActionResult> GetScreeningHistory(
        string caseId, [FromQuery] int limit,
        [FromServices] GetEarlyGovernanceScreeningHistoryQueryHandler handler,
        CancellationToken cancellationToken)
    {
        if (!CanAccessConsolidatedCase(caseId)) return StatusCode(StatusCodes.Status403Forbidden);
        return Ok(await handler.HandleAsync(new GetEarlyGovernanceScreeningHistoryQuery(caseId, NormalizeLimit(limit)), cancellationToken));
    }

    [HttpGet("referrals/history/cases/{caseId}")]
    public async Task<IActionResult> GetReferralHistory(
        string caseId, [FromQuery] int limit,
        [FromServices] GetEarlyGovernanceReferralHistoryQueryHandler handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new GetEarlyGovernanceReferralHistoryQuery(caseId, NormalizeLimit(limit)), cancellationToken));

    [HttpPost("referrals/{referralId:guid}/deliver")]
    public async Task<IActionResult> DeliverReferral(
        Guid referralId,
        [FromServices] DeliverEarlyGovernanceReferralCommandHandler handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new DeliverEarlyGovernanceReferralCommand(referralId), cancellationToken));

    [HttpGet("complaints/{assessmentId:guid}")]
    public async Task<IActionResult> GetComplaintAssessment(
        Guid assessmentId,
        [FromServices] GetComplaintClassificationAssessmentQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetComplaintClassificationAssessmentQuery(assessmentId), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("complaints/history/cases/{caseId}")]
    public async Task<IActionResult> GetComplaintHistory(
        string caseId, [FromQuery] int limit,
        [FromServices] GetComplaintClassificationHistoryQueryHandler handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new GetComplaintClassificationHistoryQuery(caseId, NormalizeLimit(limit)), cancellationToken));

    [HttpPost("pp1/seeds")]
    public async Task<IActionResult> ApplyPp1Seed(
        [FromBody] ApplyPp1SeedScenarioCommand command,
        [FromServices] ApplyPp1SeedScenarioCommandHandler? handler,
        CancellationToken cancellationToken)
    {
        if (handler is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "PP1 synthetic integration is not enabled.");
        return Ok(await handler.HandleAsync(command, cancellationToken));
    }

    /// <summary>
    /// Executes the multi-engine governance screening orchestration against an immutable snapshot.
    /// Idempotent: identical case snapshot returns existing consolidated result without re-evaluating.
    /// </summary>
    [HttpPost("screening/orchestrate")]
    [ProducesResponseType(typeof(ConsolidatedGovernanceScreeningResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> OrchestrateScreening(
        [FromBody] GovernanceScreeningCaseInputDto input,
        [FromServices] OrchestrateGovernanceScreeningCommandHandler handler,
        CancellationToken cancellationToken)
    {
        if (input is null)
        {
            return BadRequest("Screening case input cannot be null.");
        }
        if (!CanAccessConsolidatedCase(input.CaseId, dispatch: true)) return StatusCode(StatusCodes.Status403Forbidden);

        try
        {
            var result = await handler.HandleAsync(new OrchestrateGovernanceScreeningCommand(input), cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Retrieves the latest consolidated screening result for a case.
    /// </summary>
    [HttpGet("screening/cases/{caseId}/latest")]
    [ProducesResponseType(typeof(ConsolidatedGovernanceScreeningResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestScreeningForCase(
        string caseId,
        [FromServices] IConsolidatedGovernanceScreeningStore store,
        CancellationToken cancellationToken)
    {
        if (!CanAccessConsolidatedCase(caseId)) return StatusCode(StatusCodes.Status403Forbidden);
        var result = await store.GetLatestByCaseIdAsync(caseId, cancellationToken);
        return result is null ? NotFound($"No screening assessment found for case '{caseId}'.") : Ok(result);
    }

    /// <summary>
    /// Retrieves the consolidated screening history for a case.
    /// </summary>
    [HttpGet("screening/cases/{caseId}/history-consolidated")]
    [ProducesResponseType(typeof(IReadOnlyList<ConsolidatedGovernanceScreeningResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScreeningHistoryForCase(
        string caseId,
        [FromQuery] int limit,
        [FromServices] IConsolidatedGovernanceScreeningStore store,
        CancellationToken cancellationToken)
    {
        if (!CanAccessConsolidatedCase(caseId)) return StatusCode(StatusCodes.Status403Forbidden);
        var history = await store.GetHistoryByCaseIdAsync(caseId, NormalizeLimit(limit), cancellationToken);
        return Ok(history);
    }

    // Consolidated responses contain case evidence and complaints. Public fixture endpoints
    // remain separate; Development headers never authorize access to persisted case results.
    private bool CanAccessConsolidatedCase(string caseId, bool dispatch = false)
    {
        if (User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(caseId) ||
            !User.HasClaim("authority_case_id", caseId) ||
            !Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor) || actor == Guid.Empty ||
            !long.TryParse(User.FindFirstValue("exp"), out var expiry) || expiry <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;
        // Human officer requests use C3's completeness/snapshot checks. A direct peer dispatch
        // needs an explicitly issued service capability, not the general workspace capability.
        if (dispatch) return User.HasClaim("capability", "ConsolidatedScreeningDispatcher");
        return (User.IsInRole("Officer") || User.IsInRole("ComplianceOfficer") || User.IsInRole("LandOfficer")) &&
            User.HasClaim("capability", "CaseWorkspaceReviewer");
    }

    /// <summary>
    /// Retrieves all available development case fixtures for testing Component 4 in isolation.
    /// Explicitly marked with synthetic provenance.
    /// </summary>
    [HttpGet("screening/fixtures")]
    [ProducesResponseType(typeof(IReadOnlyList<GovernanceScreeningCaseInputDto>), StatusCodes.Status200OK)]
    public IActionResult GetDevelopmentFixtures()
    {
        return Ok(C4DevelopmentCaseFixtureProvider.GetAllFixtures());
    }

    /// <summary>
    /// Retrieves a specific development case fixture by case ID.
    /// </summary>
    [HttpGet("screening/fixtures/{caseId}")]
    [ProducesResponseType(typeof(GovernanceScreeningCaseInputDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetDevelopmentFixtureById(string caseId)
    {
        var fixture = C4DevelopmentCaseFixtureProvider.TryGetByCaseId(caseId);
        return fixture is null ? NotFound($"Fixture for case '{caseId}' not found.") : Ok(fixture);
    }

    private static int NormalizeLimit(int limit) => limit == 0 ? 100 : limit;
}

public sealed record WorkflowAnomalyRequestDto(
    string CaseId,
    IReadOnlyList<WorkflowAnomalyEvent> Events);

public sealed record AssessWorkflowAnomalyRequestDto(
    string CaseId,
    Guid WorkflowRunId,
    Guid WorkflowProjectionId);

public sealed record CalculateLeaseApprovalTimingRequestDto(
    Guid WorkflowProjectionId,
    DateTimeOffset? AssessmentAtUtc = null);
