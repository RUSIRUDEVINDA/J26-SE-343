namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

public sealed class CorrectionRecord
{
    public Guid Id { get; set; }
    public Guid LeaseCaseId { get; set; }
    public Guid RequestedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime DueDateUtc { get; set; }
    public string RequiredAction { get; set; } = "UploadSupportingDocument";
    public string ClassificationCode { get; set; } = string.Empty;
    public string? SectionId { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int Revision { get; set; }
    public List<CorrectionSubmission> Submissions { get; set; } = new();
    public string Purpose { get; set; } = "Correction";
    public string? AssignedInstitution { get; set; }
    public string? RequirementSourceReference { get; set; }
    public string? RequirementConfigurationVersion { get; set; }
    public bool TitlePending { get; set; }
    public bool? LeaseRecommended { get; set; }
    public Guid? RecommendationDecisionId { get; set; }
    public string? RecommendationReason { get; set; }
    public List<FulfillmentForwardReceipt> Forwarding { get; set; } = new();
    public Guid? RelatedConditionId { get; set; }
    public string? PayloadJson { get; set; }
    public List<GovernedFollowUpReview> FollowUpReviews { get; set; } = new();
}

public sealed record CorrectionSubmission(Guid DocumentId, Guid VersionId, Guid SubmittedBy,
    DateTime SubmittedAtUtc, string? Notes, Guid? ReviewedBy = null,
    DateTime? ReviewedAtUtc = null, string? Decision = null, string? ReviewReason = null);
public sealed record FulfillmentForwardReceipt(string Institution, Guid DocumentId, Guid VersionId, Guid ActorId, DateTime At, string Reason);
public sealed record GovernedFollowUpReview(Guid ActorId, string Institution, DateTime AtUtc, string Reason, string EvidenceReference);
