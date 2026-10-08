using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.DependencyInjection;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence.WorkflowAnomaly;

/// <summary>
/// Real transport and inference smoke test connecting the .NET client to the live FastAPI Python endpoint.
/// This verifies transport, JSON serialization/deserialization, and contract validation against the frozen model bundle.
/// It is NOT a model quality validation.
/// </summary>
public sealed class WorkflowAnomalyRealSmokeIntegrationTests
{
    private sealed record SyntheticTracePayload(
        string case_id,
        List<SyntheticEventPayload> events);

    private sealed record SyntheticEventPayload(
        int event_seq,
        string activity,
        string institution,
        string resource,
        string timestamp);

    [Fact]
    public async Task RealFastApiSmoke_CompleteSyntheticTrace_TransportsAndMapsAccurately()
    {
        // 1. Locate repository roots and required artifacts
        var repoRoot = FindRepositoryRoot();
        var mlDir = Directory.Exists(Path.Combine(repoRoot, "src", "Modules", "GovernanceIntelligence", "ML"))
            ? Path.Combine(repoRoot, "src", "Modules", "GovernanceIntelligence", "ML")
            : Path.Combine(repoRoot, "StateLandGovernance", "src", "Modules", "GovernanceIntelligence", "ML");
        var mlSrcDir = Path.Combine(mlDir, "src");
        var exampleTraceFile = Path.Combine(mlDir, "anomaly_detection", "examples", "complete_synthetic_trace.json");
        var modelBundleConfigFile = Path.Combine(mlDir, "anomaly_detection", "bundles", "workflow_anomaly_model_v1", "local_inference_config.json");

        if (!File.Exists(exampleTraceFile))
        {
            throw new FileNotFoundException($"Prerequisite synthetic trace file not found: {exampleTraceFile}");
        }

        if (!File.Exists(modelBundleConfigFile))
        {
            throw new FileNotFoundException($"Prerequisite verified bundle config not found: {modelBundleConfigFile}");
        }

        // 2. Select an unused loopback port
        int port = GetAvailablePort();
        var baseAddress = $"http://127.0.0.1:{port}";

        // 3. Start the existing FastAPI service on loopback
        var startInfo = new ProcessStartInfo
        {
            FileName = "py",
            Arguments = $"-3.13 -m uvicorn http_service:app --app-dir \"{mlSrcDir}\" --host 127.0.0.1 --port {port}",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["PYTHONPATH"] = $"{mlDir};{mlSrcDir}";

        using var process = new Process { StartInfo = startInfo };
        var outputLog = new System.Text.StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) outputLog.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) outputLog.AppendLine(e.Data); };

        try
        {
            Assert.True(process.Start(), "Failed to start Python FastAPI uvicorn process.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // 4. Poll /health until server is ready
            using var healthClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            bool isReady = false;
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited)
                {
                    throw new InvalidOperationException(
                        $"FastAPI process exited prematurely with code {process.ExitCode}. Log:\n{outputLog}");
                }

                try
                {
                    var response = await healthClient.GetAsync($"{baseAddress}/health");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        isReady = true;
                        break;
                    }
                }
                catch
                {
                    // Wait for socket to bind
                }

                await Task.Delay(250);
            }

            Assert.True(isReady, $"FastAPI service did not become healthy within 20 seconds. Log:\n{outputLog}");

            // 5. Load and parse the documented complete synthetic trace
            var traceJson = await File.ReadAllTextAsync(exampleTraceFile);
            var parsedPayload = JsonSerializer.Deserialize<SyntheticTracePayload>(traceJson)!;
            Assert.NotNull(parsedPayload);

            var domainEvents = parsedPayload.events
                .Select(e => new WorkflowAnomalyEvent(e.event_seq, e.activity, e.institution, e.resource, e.timestamp))
                .ToList();

            // 6. Direct HTTP verification for comparison
            using var directClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var directPost = new StringContent(traceJson, System.Text.Encoding.UTF8, "application/json");
            var directHttpResponse = await directClient.PostAsync($"{baseAddress}/workflow-anomaly/predict", directPost);
            Assert.Equal(HttpStatusCode.OK, directHttpResponse.StatusCode);
            var directJsonText = await directHttpResponse.Content.ReadAsStringAsync();
            using var directDocument = JsonDocument.Parse(directJsonText);
            var directRoot = directDocument.RootElement;

            double expectedScore = directRoot.GetProperty("anomaly_score").GetDouble();
            double expectedThreshold = directRoot.GetProperty("threshold").GetDouble();
            bool expectedFlagged = directRoot.GetProperty("flagged").GetBoolean();
            string expectedModelVersion = directRoot.GetProperty("model_version").GetString()!;

            // 7. Register and invoke through the .NET client
            var services = new ServiceCollection();
            services.AddLogging();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["WorkflowAnomaly:BaseUrl"] = baseAddress,
                    ["WorkflowAnomaly:TimeoutSeconds"] = "10"
                })
                .Build();

            services.AddWorkflowAnomalyClient(config);
            using var provider = services.BuildServiceProvider();
            var anomalyClient = provider.GetRequiredService<IWorkflowAnomalyClient>();

            var clientResult = await anomalyClient.PredictAnomalyAsync(parsedPayload.case_id, domainEvents);

            // 8. Assert transport fidelity, response parsing, and mapped inference values
            Assert.True(clientResult.IsSuccess, clientResult.Error?.Message);
            var prediction = Assert.IsType<WorkflowAnomalyPrediction>(clientResult.Prediction);

            Assert.Equal(parsedPayload.case_id, prediction.CaseId);
            Assert.Equal(expectedModelVersion, prediction.ModelVersion);
            Assert.Equal(expectedScore, prediction.AnomalyScore, 6);
            Assert.Equal(expectedThreshold, prediction.Threshold, 6);
            Assert.Equal(expectedFlagged, prediction.Flagged);
            Assert.False(string.IsNullOrWhiteSpace(prediction.AdvisoryNote));

            var features = prediction.FeatureValues;
            Assert.Equal(4, features.EventCount);
            Assert.Equal(2.041667, features.ElapsedDays, 5);
            Assert.Equal(23.0, features.MaxGapHours);
            Assert.Equal(16.333333, features.MeanGapHours, 5);
            Assert.Equal(4, features.UniqueActivities);
            Assert.Equal(0, features.RepeatedActivityCount);
            Assert.Equal(2, features.ResourceHandoffs);
            Assert.Equal(2, features.InstitutionSwitches);
            Assert.Equal(3, features.DistinctResources);
        }
        finally
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
                catch
                {
                    // Best effort process termination
                }
            }
        }
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "StateLandGovernance.sln")) ||
                Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        // Fallback to current working directory
        return Directory.GetCurrentDirectory();
    }
}
