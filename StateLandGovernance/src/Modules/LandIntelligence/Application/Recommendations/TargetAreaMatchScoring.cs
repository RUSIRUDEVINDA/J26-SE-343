namespace StateLandGovernance.LandIntelligence.Application.Recommendations;

public static class TargetAreaMatchScoring
{
    /// <summary>
    /// Continuous area match score using full-precision hectare values (no rounding before scoring).
    /// areaScore = max(0, 100 × (1 − |actual − target| / target)).
    /// </summary>
    public static decimal ComputeScore(decimal actualAreaHectares, decimal targetAreaHectares)
    {
        if (targetAreaHectares <= 0m)
        {
            return 0m;
        }

        var relativeDifference = Math.Abs(actualAreaHectares - targetAreaHectares) / targetAreaHectares;
        var raw = 100m * (1m - relativeDifference);
        return raw <= 0m ? 0m : raw;
    }
}
