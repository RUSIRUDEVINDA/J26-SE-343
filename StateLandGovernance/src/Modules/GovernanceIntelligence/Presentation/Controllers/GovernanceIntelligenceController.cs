using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.Queries;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;

namespace StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;

/// <summary>
/// API Controller boundary for the GovernanceIntelligence module.
/// </summary>
[ApiController]
[Route("api/governance-intelligence")]
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
}

public sealed record WorkflowAnomalyRequestDto(
    string CaseId,
    IReadOnlyList<WorkflowAnomalyEvent> Events);
