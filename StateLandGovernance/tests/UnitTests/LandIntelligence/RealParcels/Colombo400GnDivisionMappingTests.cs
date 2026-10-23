using StateLandGovernance.LandIntelligence.Application.RealParcels;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.UnitTests.LandIntelligence.RealParcels;

public sealed class Colombo400GnDivisionMappingTests
{
    private static string LoadWorkbookCsv()
    {
        var path = Colombo400GnDivisionMappingData.ResolveCsvPathForTests();
        return File.ReadAllText(path);
    }

    [Fact]
    public void Workbook_has_400_unique_cadastral_numbers_and_expected_status_counts()
    {
        var rows = Colombo400GnDivisionMappingCsv.Parse(LoadWorkbookCsv());
        var validation = Colombo400GnDivisionMappingCsv.Validate(rows);

        Assert.True(validation.IsValid);
        Assert.Equal(400, validation.RowCount);
        Assert.Equal(356, validation.StatusCounts[ParcelGnDivisionMappingStatus.SourceCodeAndMapMarkerAgree]);
        Assert.Equal(19, validation.StatusCounts[ParcelGnDivisionMappingStatus.SourceCodeAndMapMarkerDifferReview]);
        Assert.Equal(24, validation.StatusCounts[ParcelGnDivisionMappingStatus.SourceCodeOutsideColomboReview]);
        Assert.Equal(1, validation.StatusCounts[ParcelGnDivisionMappingStatus.MissingSourceGnCodeReview]);
    }

    [Fact]
    public void Agreed_mapping_displays_dcs_and_marker_match_for_520001020057()
    {
        var row = Colombo400GnDivisionMappingCsv.Parse(LoadWorkbookCsv())
            .Single(item => item.CadastralNumber == "520001020057");
        var dto = ParcelGnDivisionMappingProjector.ToPublicDto(row, DateTimeOffset.UtcNow);

        Assert.Equal(ParcelGnDivisionMappingStatus.SourceCodeAndMapMarkerAgree, dto.Status);
        Assert.Equal("Matched", dto.ReviewStatusDisplay);
        Assert.Equal(ParcelGnDivisionMappingProjector.AgreedProvenanceLabel, dto.ProvenanceLabel);
        Assert.Equal("1112125", dto.SourceGnIndex);
        Assert.Equal(
            "Homagama South, Homagama DS",
            ParcelGnDivisionMappingProjector.FormatPrimaryDisplay(dto));
        Assert.Null(dto.SourceFromAdministrativeCode);
        Assert.Null(dto.GnAtMapMarker);
    }

    [Fact]
    public void Conflicting_mapping_keeps_both_sides_and_needs_review()
    {
        var row = Colombo400GnDivisionMappingCsv.Parse(LoadWorkbookCsv())
            .Single(item => item.CadastralNumber == "520011040125");
        var dto = ParcelGnDivisionMappingProjector.ToPublicDto(row, DateTimeOffset.UtcNow);

        Assert.Equal(ParcelGnDivisionMappingStatus.SourceCodeAndMapMarkerDifferReview, dto.Status);
        Assert.Equal(ParcelGnDivisionMappingProjector.NeedsAdministrativeReviewDisplay, dto.ReviewStatusDisplay);
        Assert.Null(dto.PrimaryAssignment);
        Assert.NotNull(dto.SourceFromAdministrativeCode);
        Assert.NotNull(dto.GnAtMapMarker);
        Assert.Equal("Habarakada North", dto.SourceFromAdministrativeCode!.GnName);
        Assert.Equal("Mullegama South", dto.GnAtMapMarker!.GnName);
    }

    [Fact]
    public void Outside_colombo_code_keeps_both_sides_and_needs_review()
    {
        var row = Colombo400GnDivisionMappingCsv.Parse(LoadWorkbookCsv())
            .First(item => item.Status == ParcelGnDivisionMappingStatus.SourceCodeOutsideColomboReview);
        var dto = ParcelGnDivisionMappingProjector.ToPublicDto(row, DateTimeOffset.UtcNow);

        Assert.Equal(ParcelGnDivisionMappingProjector.NeedsAdministrativeReviewDisplay, dto.ReviewStatusDisplay);
        Assert.NotNull(dto.SourceFromAdministrativeCode);
        Assert.NotNull(dto.GnAtMapMarker);
        Assert.False(string.Equals(
            dto.SourceFromAdministrativeCode!.GnName,
            dto.GnAtMapMarker!.GnName,
            StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_source_code_row_needs_review_without_primary_assignment()
    {
        var row = Colombo400GnDivisionMappingCsv.Parse(LoadWorkbookCsv())
            .Single(item => item.Status == ParcelGnDivisionMappingStatus.MissingSourceGnCodeReview);
        var dto = ParcelGnDivisionMappingProjector.ToPublicDto(row, DateTimeOffset.UtcNow);

        Assert.Equal(ParcelGnDivisionMappingProjector.NeedsAdministrativeReviewDisplay, dto.ReviewStatusDisplay);
        Assert.Null(dto.PrimaryAssignment);
    }
}

internal static class Colombo400GnDivisionMappingData
{
    public static string ResolveCsvPathForTests()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 12 && dir is not null; depth++)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "src",
                "Modules",
                "LandIntelligence",
                "Infrastructure",
                "Persistence",
                "RealParcels",
                "Data",
                "Colombo_400_GN_Division_Mapping.csv");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Colombo_400_GN_Division_Mapping.csv was not found for unit tests.");
    }
}
