namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Mappers;

using System;
using System.Collections.Generic;

/// <summary>
/// Deterministic mapper from Sri Lankan administrative districts (25 districts) to provinces (9 provinces).
/// Never guesses unknown districts — returns null if unmapped to preserve insufficient-information semantics.
/// </summary>
public static class DistrictToProvinceMapper
{
    private static readonly Dictionary<string, string> DistrictToProvinceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Western Province
        ["Colombo"] = "Western",
        ["Gampaha"] = "Western",
        ["Kalutara"] = "Western",

        // Central Province
        ["Kandy"] = "Central",
        ["Matale"] = "Central",
        ["Nuwara Eliya"] = "Central",

        // Southern Province
        ["Galle"] = "Southern",
        ["Matara"] = "Southern",
        ["Hambantota"] = "Southern",

        // Northern Province
        ["Jaffna"] = "Northern",
        ["Kilinochchi"] = "Northern",
        ["Mannar"] = "Northern",
        ["Vavuniya"] = "Northern",
        ["Mullaitivu"] = "Northern",

        // Eastern Province
        ["Batticaloa"] = "Eastern",
        ["Ampara"] = "Eastern",
        ["Trincomalee"] = "Eastern",

        // North Western Province
        ["Kurunegala"] = "North Western",
        ["Puttalam"] = "North Western",

        // North Central Province
        ["Anuradhapura"] = "North Central",
        ["Polonnaruwa"] = "North Central",

        // Uva Province
        ["Badulla"] = "Uva",
        ["Monaragala"] = "Uva",
        ["Moneragala"] = "Uva",

        // Sabaragamuwa Province
        ["Ratnapura"] = "Sabaragamuwa",
        ["Kegalle"] = "Sabaragamuwa"
    };

    /// <summary>
    /// Returns the canonical province name for a given Sri Lankan district, or null if unknown or empty.
    /// </summary>
    public static string? TryMapDistrictToProvince(string? district)
    {
        if (string.IsNullOrWhiteSpace(district))
        {
            return null;
        }

        var normalized = district.Trim();
        return DistrictToProvinceMap.TryGetValue(normalized, out var province) ? province : null;
    }
}

