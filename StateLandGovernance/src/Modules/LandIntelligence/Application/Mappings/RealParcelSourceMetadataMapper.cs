using StateLandGovernance.LandIntelligence.Application.DTOs;

using StateLandGovernance.LandIntelligence.Application.RealParcels;



namespace StateLandGovernance.LandIntelligence.Application.Mappings;



/// <summary>

/// Maps persisted real-parcel source rows to API-safe metadata DTOs.

/// Claimant names, operator fields, and ownership-related free text are never mapped here.

/// </summary>

public static class RealParcelSourceMetadataMapper

{

    private static readonly string[] BlockedPropertyNames =

    [

        "ClaimantName",

        "ParcelId2",

        "SourceObjectId",

        "SourceGlobalId",

        "SourceFingerprint",

        "SourceZipSha256",

        "SourceLayerSha256",

        "SheetId2"

    ];



    public static CadastralSourceMetadataDto ToPublicDto(RealParcelSourceRecord record)

    {

        var landUseCode = ColomboCadastreSourceDomains.NormalizeCode(record.LandUseCode);

        var landUseEnglish = record.LandUseEnglishLabel?.Trim();

        var claimantCode = ColomboCadastreSourceDomains.NormalizeCode(record.ClaimantCategoryCode);

        var claimantEnglish = record.ClaimantCategoryEnglishLabel?.Trim();



        return new CadastralSourceMetadataDto(

            record.SourceName,

            record.SourceLayer,

            record.SourceFeatureId,

            string.IsNullOrWhiteSpace(landUseCode) ? null : landUseCode,

            landUseEnglish,

            record.LandUseSourceText,

            string.IsNullOrWhiteSpace(claimantCode) ? null : claimantCode,

            claimantEnglish,

            record.ParcelStatusCode,

            record.GnIndex,

            record.CoordinateStatus,

            record.ImportedAtUtc)

        {

            ResearchRecordId = record.ResearchRecordId,
            MapMarkerLatitude = record.MapMarkerLatitude,
            MapMarkerLongitude = record.MapMarkerLongitude,

            SourceReportedLandUseDisplay = ColomboCadastreSourceDomains.FormatSourceReportedLandUseDisplay(

                landUseCode,

                landUseEnglish),

            SourceReportedClaimantCategoryDisplay = ColomboCadastreSourceDomains.FormatSourceReportedClaimantCategoryDisplay(

                claimantCode,

                claimantEnglish),

        };

    }



    public static bool ExposesOnlySafeProperties() =>

        typeof(CadastralSourceMetadataDto).GetProperties()

            .All(property => !BlockedPropertyNames.Contains(property.Name, StringComparer.Ordinal));

}



/// <summary>

/// Internal persistence shape for real_parcel_sources reads. Not exposed outside Infrastructure mapping.

/// </summary>

public sealed record RealParcelSourceRecord(

    Guid LandParcelId,

    string SourceName,

    string SourceLayer,

    string SourceFeatureId,

    string? LandUseCode,

    string? LandUseEnglishLabel,

    string? LandUseSourceText,

    string? ClaimantCategoryCode,

    string? ClaimantCategoryEnglishLabel,

    string? ParcelStatusCode,

    string? GnIndex,

    string? CoordinateStatus,

    DateTimeOffset ImportedAtUtc,

    string? ResearchRecordId = null,

    double? MapMarkerLatitude = null,

    double? MapMarkerLongitude = null);


