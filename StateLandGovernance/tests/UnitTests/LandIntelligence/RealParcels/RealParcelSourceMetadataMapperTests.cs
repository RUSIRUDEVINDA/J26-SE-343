using StateLandGovernance.LandIntelligence.Application.Mappings;

namespace StateLandGovernance.UnitTests.LandIntelligence.RealParcels;

public sealed class RealParcelSourceMetadataMapperTests
{
    [Fact]
    public void ToPublicDto_maps_only_safe_source_fields()
    {
        var importedAt = new DateTimeOffset(2026, 9, 30, 15, 45, 0, TimeSpan.Zero);
        var record = new RealParcelSourceRecord(
            Guid.NewGuid(),
            "Survey Department/iLAS public GIS",
            "Publishing_Server/LUSE_Cadastre_2026/MapServer/0",
            "521007050089",
            "2",
            "Garden",
            "ගෙවතු",
            "3",
            "Private",
            "1",
            "42",
            "Verified",
            importedAt);

        var dto = RealParcelSourceMetadataMapper.ToPublicDto(record);

        Assert.Equal("Survey Department/iLAS public GIS", dto.SourceProvenanceLabel);
        Assert.Equal("521007050089", dto.SourceFeatureId);
        Assert.Equal("2", dto.SourceReportedLandUseCode);
        Assert.Equal("Garden", dto.SourceReportedLandUseEnglishLabel);
        Assert.Equal("ගෙවතු", dto.SourceReportedLandUseText);
        Assert.Equal("Garden", dto.SourceReportedLandUseDisplay);
        Assert.Equal("3", dto.SourceReportedClaimantCategoryCode);
        Assert.Equal("Private", dto.SourceReportedClaimantCategoryEnglishLabel);
        Assert.Equal("42", dto.GnIndex);
        Assert.Equal(importedAt, dto.ImportedAtUtc);
    }

    [Fact]
    public void PublicDto_does_not_expose_claimant_name_properties() =>
        Assert.True(RealParcelSourceMetadataMapper.ExposesOnlySafeProperties());
}
