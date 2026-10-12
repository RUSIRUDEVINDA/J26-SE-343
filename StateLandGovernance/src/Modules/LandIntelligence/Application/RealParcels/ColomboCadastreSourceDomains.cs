using System.Collections.Frozen;

namespace StateLandGovernance.LandIntelligence.Application.RealParcels;

/// <summary>
/// English coded-value domains from Colombo_GIS/metadata/Cadastre_Parcels_Landuse_classified___Land_Use_0.json
/// (Survey Department/iLAS LUSE_Cadastre_2026 MapServer layer 0).
/// </summary>
public static class ColomboCadastreSourceDomains
{
    public const string SourceProvenanceLabel = "Survey Department/iLAS public GIS";

    private static readonly FrozenDictionary<int, string> LandUseByCode = new Dictionary<int, string>
    {
        [0] = "Not Checked",
        [1] = "Homegarden",
        [2] = "Garden",
        [3] = "Deniya",
        [4] = "Rock",
        [5] = "Quarry",
        [6] = "Open Waste Land",
        [7] = "Patana",
        [8] = "Marshy Land",
        [9] = "Barren Land",
        [10] = "Reserves",
        [11] = "Cemetery",
        [12] = "Reservation",
        [13] = "Other",
        [14] = "Earth Drain",
        [15] = "Masonry Drain",
        [16] = "Bund",
        [17] = "Bank",
        [20] = "Hydro",
        [21] = "Canal",
        [22] = "Ela",
        [23] = "Oya",
        [24] = "Reservoir",
        [25] = "Stream",
        [26] = "Tank",
        [27] = "Water hole",
        [28] = "River",
        [29] = "Trench",
        [30] = "Channel",
        [31] = "Water Course",
        [32] = "Kadura",
        [39] = "Other Hydro",
        [40] = "Forest",
        [41] = "Jungle",
        [42] = "High Jungle",
        [43] = "Low Jungle",
        [44] = "New Clearing",
        [49] = "Other Jungle",
        [50] = "Path",
        [51] = "Foot Path",
        [52] = "Means of Access",
        [53] = "Road",
        [54] = "Road(V.C.)",
        [55] = "Road(M.C.)",
        [56] = "Road(U.C.)",
        [57] = "Road(Estate)",
        [58] = "Road(P.S.)",
        [59] = "Road(R.D.A.)",
        [60] = "Road(P.R.D.A.)",
        [61] = "Road(Private)",
        [62] = "Railway Road",
        [63] = "Cart Track",
        [64] = "Main Road",
        [69] = "Other Road",
        [70] = "Estate",
        [71] = "Chena",
        [72] = "Ovita",
        [73] = "Paddy Field",
        [74] = "Tea",
        [75] = "Coconut",
        [76] = "Rubber",
        [77] = "Cinnamon",
        [78] = "Coffee",
        [79] = "Cocoa",
        [80] = "Tobacco",
        [89] = "Other Cultivation",
        [90] = "Rock",
        [91] = "Quarry",
        [92] = "Graphite Mine",
        [99] = "Other Earth Resource",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<int, string> ClaimantCategoryByCode = new Dictionary<int, string>
    {
        [1] = "Not Checked",
        [2] = "State",
        [3] = "Private",
        [4] = "Not Known",
        [5] = "Other Authority",
        [6] = "Unsettled",
        [7] = "Other Authority - LRC",
        [8] = "Other Authority - Mahaweli",
        [9] = "Other Authority - NHDA",
        [10] = "Other Authority - Electricity Board",
        [11] = "Other Authority - UGC",
        [12] = "Other Authority - Divisional Council",
        [13] = "Other Authority - UDA",
        [14] = "Other Authority - Municipal Council",
        [15] = "Other Authority - Town Council",
        [16] = "Other Authority - Provincial Council",
        [17] = "Other Authority - RDA",
        [18] = "Other Authority - Railway Department",
        [19] = "Praveni",
        [20] = "Vihara/Dewalagam",
        [30] = "Other",
    }.ToFrozenDictionary();

    public static string NormalizeCode(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim();

    public static bool TryParseLandUseCode(string? raw, out int code) =>
        int.TryParse(NormalizeCode(raw), out code);

    public static bool TryParseClaimantCategoryCode(string? raw, out int code) =>
        int.TryParse(NormalizeCode(raw), out code);

    public static string? TryDecodeLandUseEnglishLabel(int code) =>
        LandUseByCode.TryGetValue(code, out var label) ? label : null;

    public static string? TryDecodeLandUseEnglishLabel(string? rawCode) =>
        TryParseLandUseCode(rawCode, out var code) ? TryDecodeLandUseEnglishLabel(code) : null;

    public static string FormatUnknownLandUseCode(string code) =>
        $"Source code {NormalizeCode(code)} — label unavailable";

    public static string FormatSourceReportedLandUseDisplay(string? code, string? englishLabel)
    {
        if (!string.IsNullOrWhiteSpace(englishLabel))
        {
            return englishLabel.Trim();
        }

        var normalizedCode = NormalizeCode(code);
        if (!string.IsNullOrWhiteSpace(normalizedCode))
        {
            return FormatUnknownLandUseCode(normalizedCode);
        }

        return "Unavailable";
    }

    public static string? TryDecodeClaimantCategoryEnglishLabel(int code) =>
        ClaimantCategoryByCode.TryGetValue(code, out var label) ? label : null;

    public static string? TryDecodeClaimantCategoryEnglishLabel(string? rawCode) =>
        TryParseClaimantCategoryCode(rawCode, out var code) ? TryDecodeClaimantCategoryEnglishLabel(code) : null;

    public static string FormatUnknownClaimantCategoryCode(string code) =>
        $"Source code {NormalizeCode(code)} — label unavailable";

    public static string FormatSourceReportedClaimantCategoryDisplay(string? code, string? englishLabel)
    {
        if (!string.IsNullOrWhiteSpace(englishLabel))
        {
            return englishLabel.Trim();
        }

        var normalizedCode = NormalizeCode(code);
        if (!string.IsNullOrWhiteSpace(normalizedCode))
        {
            return FormatUnknownClaimantCategoryCode(normalizedCode);
        }

        return "Unavailable";
    }

    public static IReadOnlyList<(string Code, string EnglishLabel)> ListLandUseFilterOptions() =>
        LandUseByCode
            .OrderBy(pair => pair.Key)
            .Select(pair => (pair.Key.ToString(), pair.Value))
            .ToList();
}
