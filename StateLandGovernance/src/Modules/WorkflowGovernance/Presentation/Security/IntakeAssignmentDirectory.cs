namespace StateLandGovernance.WorkflowGovernance.Presentation.Security;
using Microsoft.Extensions.Configuration;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

/// <summary>Trusted identity/office directory, never request-supplied roles or scopes.</summary>
public sealed class IntakeAssignmentDirectory(IConfiguration configuration) : IIntakeAssignmentDirectory
{
    public IntakeCoordinatorScope? Coordinator(Guid actorId) => configuration.GetSection("WorkflowGovernance:IntakeCoordinators")
        .GetChildren().Select(x => new IntakeCoordinatorScope(Guid.TryParse(x["ActorId"], out var id) ? id : Guid.Empty,
            x["OfficeCode"] ?? "", bool.TryParse(x["CanManageUnassigned"], out var unassigned) && unassigned,
            DateTimeOffset.TryParse(x["ExpiresAtUtc"], out var until) ? until : DateTimeOffset.MinValue, x["SourceReference"] ?? ""))
        .SingleOrDefault(x => x.ActorId == actorId && x.OfficeCode.Length > 0);
    public IReadOnlyList<EligibleCaseReviewer> Reviewers => configuration.GetSection("WorkflowGovernance:IntakeReviewers")
        .GetChildren().Where(x => Guid.TryParse(x["ActorId"], out _) && !string.IsNullOrWhiteSpace(x["OfficeCode"]))
        .Select(x => new EligibleCaseReviewer(Guid.Parse(x["ActorId"]!), x["DisplayName"] ?? "Reviewer", x["OfficeCode"]!)).ToArray();
}
