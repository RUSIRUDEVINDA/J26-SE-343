using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;

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

    public GovernanceIntelligenceController(
        EvaluateComplianceCommandHandler complianceHandler,
        DetectConflictsCommandHandler conflictHandler,
        EvaluateGovernanceRiskCommandHandler riskHandler,
        GenerateGovernanceExplanationCommandHandler explanationHandler,
        EvaluateGovernanceConsensusCommandHandler consensusHandler,
        EvaluateConditionalVerificationCommandHandler verificationHandler)
    {
        _complianceHandler = complianceHandler;
        _conflictHandler = conflictHandler;
        _riskHandler = riskHandler;
        _explanationHandler = explanationHandler;
        _consensusHandler = consensusHandler;
        _verificationHandler = verificationHandler;
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
}
