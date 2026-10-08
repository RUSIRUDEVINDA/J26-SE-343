using System.Threading;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Integrations;

/// <summary>
/// Process-wide experimental ML predict-call counter for local verification.
/// When LAND_INTELLIGENCE_EXPERIMENTAL_ML_CALL_LOG is set, the count is written to that file after each predict attempt.
/// </summary>
public static class ExperimentalColomboMlCallLog
{
    private static long _predictAttempts;

    public static long PredictAttempts => Interlocked.Read(ref _predictAttempts);

    public static void Reset() => Interlocked.Exchange(ref _predictAttempts, 0);

    public static void RecordPredictAttempt()
    {
        var count = Interlocked.Increment(ref _predictAttempts);
        var path = Environment.GetEnvironmentVariable("LAND_INTELLIGENCE_EXPERIMENTAL_ML_CALL_LOG");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            File.WriteAllText(path, count.ToString());
        }
        catch
        {
            // Best-effort diagnostics only.
        }
    }
}
