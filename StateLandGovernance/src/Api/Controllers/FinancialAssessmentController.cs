using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.LeaseFeasibility.Domain.Exceptions;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;
using StateLandGovernance.LeaseFeasibility.Domain.Services;
using StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;
using StateLandGovernance.LeaseFeasibility.Infrastructure.Services;

namespace StateLandGovernance.Api.Controllers;

public sealed record ReviewedAssessmentRequest(decimal? Income, decimal? Balance, decimal? Debt,
    decimal? Lease, decimal? Consistency, int? Overdrafts, string Grade, bool? DefaultHistory, bool Confirmed);

[ApiController]
[Route("api/lease-feasibility/assessment")]
public sealed class FinancialAssessmentController : ControllerBase
{
    [HttpPost]
    public IActionResult Assess(ReviewedAssessmentRequest request)
    {
        if (!request.Confirmed || request.Income == null || request.Balance == null || request.Debt == null ||
            request.Lease == null || request.Consistency == null || request.Overdrafts == null || request.DefaultHistory == null)
            return Problem(statusCode: 400, detail: "Review and confirm every financial field, including overdrafts and default history. Missing evidence cannot be treated as zero or false.");
        var normalized = CribReportParser.NormalizeGrade(request.Grade);
        if (normalized == null || !Enum.TryParse<CreditRiskGrade>(normalized, out var grade))
            return Problem(statusCode: 400, detail: "A verified CRIB grade A–E is required. XX or missing credit evidence requires manual review.");
        try
        {
            var input = new FinancialFeasibilityScoringInput("session-review", "session-applicant",
                request.Income.Value, request.Consistency.Value, request.Lease.Value, request.Debt.Value,
                request.Balance.Value, request.Overdrafts.Value, grade, request.DefaultHistory.Value);
            var result = new FinancialFeasibilityScoringEngine().EvaluateFeasibility(input, DateTimeOffset.UtcNow);
            Response.Headers.CacheControl = "no-store";
            return Ok(new { grade = result.Grade.ToString(), action = result.Action.ToString(),
                result.ContractVersion, result.ScoreBreakdown, result.GeneratedAt });
        }
        catch (InvalidFinancialProfileException ex) { return Problem(statusCode: 400, detail: ex.Message); }
        catch (OverflowException) { return Problem(statusCode: 400, detail: "Financial amounts exceed the supported range."); }
    }
}
