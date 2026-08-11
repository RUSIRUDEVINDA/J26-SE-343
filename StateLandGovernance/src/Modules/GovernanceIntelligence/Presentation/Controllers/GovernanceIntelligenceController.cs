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

    public GovernanceIntelligenceController(
        EvaluateComplianceCommandHandler complianceHandler,
        DetectConflictsCommandHandler conflictHandler)
    {
        _complianceHandler = complianceHandler;
        _conflictHandler = conflictHandler;
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
}
