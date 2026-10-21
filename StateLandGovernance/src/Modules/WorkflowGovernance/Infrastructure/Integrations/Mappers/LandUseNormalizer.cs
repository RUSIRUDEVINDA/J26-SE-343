namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Mappers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Deterministic normalizer for proposed land use text into Component 4 recognized categories:
/// Commercial, Agricultural, Industrial, Residential, Conservation, Ecotourism.
/// 
/// Ambiguity Safeguards:
/// - A value matching multiple incompatible canonical categories (e.g. 'commercial agriculture',
///   'commercial eco-tourism and conservation project') returns null (ambiguous) rather than
///   arbitrarily selecting the first match.
/// - Unrecognized or empty text returns null (never guesses).
/// </summary>
public static class LandUseNormalizer
{
    private static readonly (string Category, Regex Pattern)[] CategoryMatchers = new[]
    {
        ("Ecotourism", new Regex(@"\b(?:eco[\s-]?tourism|eco[\s-]?resort|nature\s+resort)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Conservation", new Regex(@"\b(?:conservation|nature\s+reserve|wildlife\s+sanctuary|forest\s+preserv\w*)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Agricultural", new Regex(@"\b(?:agricultur\w*|farming|paddy|cultivat\w*|crop\w*|dairy)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Industrial", new Regex(@"\b(?:industr\w*|manufacturing|factory|processing|warehouse)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Residential", new Regex(@"\b(?:residential|housing|dwelling|apartments?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Commercial", new Regex(@"\b(?:commercial|business|retail|hotel|hospitality)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
    };

    /// <summary>
    /// Normalizes proposed land use to a canonical C4 category, or null if unrecognized or ambiguous.
    /// </summary>
    public static string? TryNormalizeLandUse(string? proposedUse)
    {
        if (string.IsNullOrWhiteSpace(proposedUse))
        {
            return null;
        }

        var text = proposedUse.Trim();
        var matchingCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (category, pattern) in CategoryMatchers)
        {
            if (pattern.IsMatch(text))
            {
                matchingCategories.Add(category);
            }
        }

        // If multiple distinct categories match, value is ambiguous -> fail safe by returning null
        if (matchingCategories.Count > 1)
        {
            return null;
        }

        // If exactly one category matched, return it
        if (matchingCategories.Count == 1)
        {
            return matchingCategories.First();
        }

        return null;
    }
}
