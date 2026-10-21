namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Mappers;
// Existing C4 adapter contract retains its four-decimal normalization.
public static class LandExtentNormalizer
{
    public static decimal? TryNormalizeToHectares(string? extent, string? unit = null)
    {
        var value = Application.Mappings.LandExtentNormalizer.TryNormalizeToHectares(extent, unit);
        return value.HasValue ? Math.Round(value.Value, 4, MidpointRounding.AwayFromZero) : null;
    }
}
