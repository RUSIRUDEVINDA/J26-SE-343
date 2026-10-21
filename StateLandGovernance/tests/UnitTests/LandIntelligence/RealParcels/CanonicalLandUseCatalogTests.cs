using StateLandGovernance.LandIntelligence.Application.RealParcels;

namespace StateLandGovernance.UnitTests.LandIntelligence.RealParcels;

public sealed class CanonicalLandUseCatalogTests
{
    [Fact]
    public void Catalog_exposes_exactly_the_ten_canonical_options_in_display_order() =>
        Assert.Equal(
            [
                "Coconut", "Wetland", "Scrub / Low Jungle", "Paddy Field", "Homegarden",
                "Garden", "Open Waste Land", "Barren Land", "Rubber", "Other",
            ],
            CanonicalLandUseCatalog.Definitions.Select(d => d.Label).ToArray());

    [Fact]
    public void Catalog_source_codes_and_labels_exist_in_cadastre_domain()
    {
        foreach (var definition in CanonicalLandUseCatalog.Definitions)
        {
            Assert.Equal(definition.SourceLandUseCodes.Count, definition.SourceLandUseLabels.Count);
            for (var i = 0; i < definition.SourceLandUseCodes.Count; i++)
            {
                Assert.Equal(
                    definition.SourceLandUseLabels[i],
                    ColomboCadastreSourceDomains.TryDecodeLandUseEnglishLabel(definition.SourceLandUseCodes[i]));
            }
        }
    }

    [Fact]
    public void A_source_code_or_gis_group_maps_to_at_most_one_canonical_option()
    {
        var codes = CanonicalLandUseCatalog.Definitions.SelectMany(d => d.SourceLandUseCodes).ToList();
        var groups = CanonicalLandUseCatalog.Definitions.SelectMany(d => d.GisSupplementGroups).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.Equal(groups.Count, groups.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("Coconut", "Coconut")]
    [InlineData("coconut", "Coconut")]
    [InlineData("Scrub / Low Jungle", "Scrub / Low Jungle")]
    [InlineData("Scrub / low jungle", "Scrub / Low Jungle")]
    [InlineData("scrub-low-jungle", "Scrub / Low Jungle")]
    [InlineData("Other State parcel", "Other")]
    [InlineData("Paddy Field", "Paddy Field")]
    public void TryResolve_accepts_key_label_and_legacy_gis_group(string input, string expectedLabel) =>
        Assert.Equal(expectedLabel, CanonicalLandUseCatalog.TryResolve(input)?.Label);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Tea")]
    public void TryResolve_rejects_unknown_values(string? input) =>
        Assert.Null(CanonicalLandUseCatalog.TryResolve(input));

    [Theory]
    [InlineData("75", null, "Coconut")]
    [InlineData("8", "Marshy Land", "Wetland")]
    [InlineData("43", null, "Scrub / Low Jungle")]
    [InlineData("73", null, "Paddy Field")]
    [InlineData("1", null, "Homegarden")]
    [InlineData("2", null, "Garden")]
    [InlineData("6", null, "Open Waste Land")]
    [InlineData("9", null, "Barren Land")]
    [InlineData("76", null, "Rubber")]
    [InlineData("13", null, "Other")]
    [InlineData(null, "Rubber", "Rubber")]
    public void TryMapSource_maps_original_cadastral_labels(string? code, string? label, string expected) =>
        Assert.Equal(expected, CanonicalLandUseCatalog.TryMapSource(code, label)?.Label);

    [Theory]
    [InlineData("0")]
    [InlineData("74")]
    public void TryMapSource_ignores_unmapped_labels(string code) =>
        Assert.Null(CanonicalLandUseCatalog.TryMapSource(code, null));

    [Fact]
    public void Coconut_union_counts_each_parcel_once_and_yields_23_for_seeded_fixture()
    {
        // Seeded fixture: 20 GIS-classified Coconut parcels; 6 original Coconut (code 75) parcels,
        // of which 3 are also GIS-classified. Union = 20 + 6 - 3 = 23, never 26.
        var gisOnly = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToList();
        var overlap = gisOnly.Take(3).ToList();
        var sourceOnly = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();

        var evidence = new List<CanonicalLandUseParcelEvidence>();
        evidence.AddRange(gisOnly.Select(id => new CanonicalLandUseParcelEvidence(id, null, null, "Coconut")));
        evidence.AddRange(overlap.Concat(sourceOnly)
            .Select(id => new CanonicalLandUseParcelEvidence(id, "75", "Coconut", null)));

        var coconut = CanonicalLandUseCatalog.CountDistinctParcels(evidence).Single(c => c.Label == "Coconut");

        Assert.Equal(23, coconut.ParcelCount);
        Assert.Equal(6, coconut.SourceMatchedParcelCount);
        Assert.Equal(20, coconut.GisMatchedParcelCount);
        Assert.Equal(3, coconut.BothSourcesParcelCount);
    }

    [Fact]
    public void Parcel_with_original_label_and_gis_group_in_one_row_is_counted_once()
    {
        var id = Guid.NewGuid();
        var counts = CanonicalLandUseCatalog.CountDistinctParcels(
            [new CanonicalLandUseParcelEvidence(id, "75", "Coconut", "Coconut")]);

        Assert.Equal(1, counts.Single(c => c.Label == "Coconut").ParcelCount);
        Assert.Equal(1, counts.Single(c => c.Label == "Coconut").BothSourcesParcelCount);
    }

    [Fact]
    public void Duplicate_rows_for_the_same_parcel_do_not_inflate_counts()
    {
        var id = Guid.NewGuid();
        var counts = CanonicalLandUseCatalog.CountDistinctParcels(
        [
            new CanonicalLandUseParcelEvidence(id, "2", "Garden", null),
            new CanonicalLandUseParcelEvidence(id, "2", "Garden", null),
        ]);

        Assert.Equal(1, counts.Single(c => c.Label == "Garden").ParcelCount);
    }

    [Fact]
    public void Original_label_and_gis_classification_may_map_to_different_options()
    {
        // Original cadastral label stays "Garden"; the GIS supplement says Wetland. Both are reported separately.
        var id = Guid.NewGuid();
        var counts = CanonicalLandUseCatalog.CountDistinctParcels(
            [new CanonicalLandUseParcelEvidence(id, "2", "Garden", "Wetland")]);

        Assert.Equal(1, counts.Single(c => c.Label == "Garden").ParcelCount);
        Assert.Equal(1, counts.Single(c => c.Label == "Wetland").ParcelCount);
        Assert.Equal(0, counts.Single(c => c.Label == "Wetland").SourceMatchedParcelCount);
    }

    [Fact]
    public void Every_canonical_option_is_returned_including_zero_counts() =>
        Assert.All(
            CanonicalLandUseCatalog.CountDistinctParcels([]),
            count => Assert.Equal(0, count.ParcelCount));
}
