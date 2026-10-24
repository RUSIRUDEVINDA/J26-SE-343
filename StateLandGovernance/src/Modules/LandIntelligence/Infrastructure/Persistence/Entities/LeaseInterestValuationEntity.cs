namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

/// <summary>Officer-recorded observations and assessed value. No lease rent is inferred from this record.</summary>
public sealed class LeaseInterestValuationEntity
{
    public Guid Id { get; set; }
    public Guid LeaseInterestRequestId { get; set; }
    public Guid LandParcelId { get; set; }
    public int Revision { get; set; }
    public string FormType { get; set; } = string.Empty;
    public decimal AssessedLandValueLkr { get; set; }
    public decimal? AssessedAreaHectares { get; set; }
    public DateOnly? InspectionDate { get; set; }
    public DateOnly? ValuationDate { get; set; }
    public string? ValuationReference { get; set; }
    public string? IssuingOffice { get; set; }
    public string? SiteCondition { get; set; }
    public string? CultivationDetails { get; set; }
    public string? ResidentialImprovements { get; set; }
    public string? ProjectUseDetails { get; set; }
    public string? OtherTypeDetails { get; set; }
    public string? OfficerNotes { get; set; }
    public string? WorksheetJson { get; set; }

    /// <summary>
    /// Client-generated key for a single save attempt. Unique per request so retries return the same revision.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public Guid RecordedByOfficerId { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
}
