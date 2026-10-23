namespace StateLandGovernance.LandIntelligence.Domain.Enums;

/// <summary>
/// Colombo 400 GN division workbook mapping status (research dataset only).
/// </summary>
public enum ParcelGnDivisionMappingStatus
{
    SourceCodeAndMapMarkerAgree = 1,
    SourceCodeAndMapMarkerDifferReview = 2,
    SourceCodeOutsideColomboReview = 3,
    MissingSourceGnCodeReview = 4,
}
