namespace StateLandGovernance.WorkflowGovernance.Presentation.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class CaseManagementExceptionFilterAttribute : ExceptionFilterAttribute, IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        var status = context.Result switch { ObjectResult o => o.StatusCode, StatusCodeResult r => r.StatusCode, _ => null };
        if (status != 403) return;
        var authenticated = context.HttpContext.User.Identity?.IsAuthenticated == true;
        var problem = new ProblemDetails { Status = authenticated ? 403 : 401,
            Title = authenticated ? "Required access or assignment" : "Sign in required",
            Detail = authenticated ? "Your authenticated identity needs the required case, document or institution assignment." : "Sign in with your assigned identity to continue." };
        problem.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        context.Result = new ObjectResult(problem) { StatusCode = problem.Status };
    }
    public void OnResultExecuted(ResultExecutedContext context) { }

    public override void OnException(ExceptionContext context)
    {
        var status = context.Exception switch
        {
            LeaseCaseNotFoundException or GovernedDocumentNotFoundException => 404,
            UnauthorizedAccessException or Domain.Exceptions.MissingVerifiedAuthorityException or Domain.Exceptions.InvalidAuthoritySnapshotException => 403,
            LeaseCaseConcurrencyException or DocumentConcurrencyException or InvalidOperationException => 409,
            ArgumentException or ValidationException => 400,
            Domain.Exceptions.WorkflowGovernanceDomainException => 409,
            _ => 0
        };
        if (status == 0) return;
        context.Result = new ObjectResult(new ProblemDetails { Status = status,
            Title = status == 409 ? "Case state conflict" : "Request cannot be completed", Detail = context.Exception.Message })
            { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
