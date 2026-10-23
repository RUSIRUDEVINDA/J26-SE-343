using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

public sealed class LeaseInterestRequestEntity
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid LandParcelId { get; set; }

    public LeaseInterestRequestStatus Status { get; set; }

    public string? AcknowledgementReference { get; set; }

    public string? SubmitIdempotencyKey { get; set; }

    public string PayloadJson { get; set; } = "{}";

    public int RequestedPurpose { get; set; }

    public int? RequestedDurationYears { get; set; }

    public bool ConsentToPreliminaryAssessment { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? SubmittedAtUtc { get; set; }

    /// <summary>AES-GCM encrypted normalized NIC (base64). Not stored in PayloadJson.</summary>
    public string? ApplicantNicCiphertext { get; set; }

    /// <summary>HMAC digest of normalized NIC for exact-match officer lookup.</summary>
    public string? ApplicantNicLookupDigest { get; set; }

    /// <summary>HMAC digest of normalized BRN for organization applicant officer lookup.</summary>
    public string? ApplicantBrnLookupDigest { get; set; }

    public ICollection<LeaseInterestStatusHistoryEntity> StatusHistory { get; set; } =
        new List<LeaseInterestStatusHistoryEntity>();
}
