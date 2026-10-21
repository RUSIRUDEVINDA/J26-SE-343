using System.Text.Json.Serialization;
using StateLandGovernance.LandIntelligence.Application.Serialization;

namespace StateLandGovernance.LandIntelligence.Application.DTOs;

/// <summary>Officer-entered worksheet values. Monetary fields are never calculated by this API.</summary>
public sealed class LeaseWorksheetPeriodLineDto
{
    public string Period { get; init; } = string.Empty;

    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? AmountLkr { get; init; }
}

public sealed class LeaseWorksheetDetailsDto
{
    public string? LeaseFileNumber { get; init; }
    public string? DivisionalSecretariat { get; init; }

    [JsonConverter(typeof(FlexibleNullableDateOnlyConverter))]
    public DateOnly? LeaseApprovalDate { get; init; }

    [JsonConverter(typeof(FlexibleNullableDateOnlyConverter))]
    public DateOnly? LeaseCommencementDate { get; init; }

    [JsonConverter(typeof(FlexibleNullableDateOnlyConverter))]
    public DateOnly? ValuationReportDate { get; init; }

    [JsonConverter(typeof(FlexibleNullableDateOnlyConverter))]
    public DateOnly? LeaseAgreementDate { get; init; }

    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? PreviousAssessedValueLkr { get; init; }

    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? LeaseRatePercent { get; init; }

    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? AnnualLeaseAmountLkr { get; init; }

    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? FirstPeriodAmountLkr { get; init; }

    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? SecondPeriodAmountLkr { get; init; }

    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? SecondPeriodAnnualAmountLkr { get; init; }

    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? Circular2018AmountLkr { get; init; }

    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? Circular2020SixAdjustmentLkr { get; init; }

    public IReadOnlyList<LeaseWorksheetPeriodLineDto>? FirstPeriodLines { get; init; }
    public IReadOnlyList<LeaseWorksheetPeriodLineDto>? SecondPeriodLines { get; init; }
    public string? WorksheetNotes { get; init; }
}

public sealed class RecordLeaseInterestValuationDto
{
    public string FormType { get; init; } = string.Empty;

    public decimal AssessedLandValueLkr { get; init; }

    [JsonConverter(typeof(FlexibleNullableDecimalConverter))]
    public decimal? AssessedAreaHectares { get; init; }

    [JsonConverter(typeof(FlexibleNullableDateOnlyConverter))]
    public DateOnly? InspectionDate { get; init; }

    [JsonConverter(typeof(FlexibleNullableDateOnlyConverter))]
    public DateOnly? ValuationDate { get; init; }

    public string? ValuationReference { get; init; }
    public string? IssuingOffice { get; init; }
    public string? SiteCondition { get; init; }
    public string? CultivationDetails { get; init; }
    public string? ResidentialImprovements { get; init; }
    public string? ProjectUseDetails { get; init; }
    public string? OtherTypeDetails { get; init; }
    public string? OfficerNotes { get; init; }
    public LeaseWorksheetDetailsDto Worksheet { get; init; } = new();
}

public sealed class LeaseInterestValuationDto
{
    public Guid Id { get; init; }
    public Guid LeaseInterestRequestId { get; init; }
    public Guid LandParcelId { get; init; }
    public int Revision { get; init; }
    public string FormType { get; init; } = string.Empty;
    public decimal AssessedLandValueLkr { get; init; }
    public decimal? AssessedAreaHectares { get; init; }
    public DateOnly? InspectionDate { get; init; }
    public DateOnly? ValuationDate { get; init; }
    public string? ValuationReference { get; init; }
    public string? IssuingOffice { get; init; }
    public string? SiteCondition { get; init; }
    public string? CultivationDetails { get; init; }
    public string? ResidentialImprovements { get; init; }
    public string? ProjectUseDetails { get; init; }
    public string? OtherTypeDetails { get; init; }
    public string? OfficerNotes { get; init; }
    public LeaseWorksheetDetailsDto? Worksheet { get; init; }
    public string IdempotencyKey { get; init; } = string.Empty;
    public Guid RecordedByOfficerId { get; init; }
    public DateTimeOffset RecordedAtUtc { get; init; }
}

/// <summary>
/// Applicant-visible summary of the latest officer valuation. Excludes officer notes and internal worksheet detail.
/// Does not change request status; recording a valuation is not a lease approval.
/// </summary>
public sealed class LeaseInterestApplicantValuationSummaryDto
{
    public int Revision { get; init; }
    public DateTimeOffset RecordedAtUtc { get; init; }
    public string FormType { get; init; } = string.Empty;
    public decimal AssessedLandValueLkr { get; init; }
    public string? LeaseFileNumber { get; init; }
    public string? DivisionalSecretariat { get; init; }
}
