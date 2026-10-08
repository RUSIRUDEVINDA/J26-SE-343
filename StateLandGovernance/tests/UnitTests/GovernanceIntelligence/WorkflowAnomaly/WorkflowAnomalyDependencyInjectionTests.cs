using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.WorkflowAnomaly;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence.WorkflowAnomaly;

public sealed class WorkflowAnomalyDependencyInjectionTests
{
    private static readonly IReadOnlyList<WorkflowAnomalyEvent> MinimalEvents = new List<WorkflowAnomalyEvent>
    {
        new(1, "DS Application Received", "DS", "Demo Officer 1", "2031-02-03T08:00:00")
    };

    [Fact]
    public void AddWorkflowAnomalyClient_ValidConfiguration_ResolvesTypedApplicationClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = ConfigurationWith(
            ("WorkflowAnomaly:BaseUrl", "http://127.0.0.1:8104"),
            ("WorkflowAnomaly:TimeoutSeconds", "8"),
            ("WorkflowAnomaly:MaxEvents", "300"));

        services.AddWorkflowAnomalyClient(configuration);
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var applicationClient = provider.GetRequiredService<IWorkflowAnomalyClient>();
        var options = provider.GetRequiredService<IOptions<WorkflowAnomalyOptions>>().Value;

        Assert.IsType<HttpWorkflowAnomalyClient>(applicationClient);
        Assert.Equal("http://127.0.0.1:8104", options.BaseUrl);
        Assert.Equal(8, options.TimeoutSeconds);
        Assert.Equal(300, options.MaxEvents);
    }

    [Theory]
    [InlineData(null, "10", "500")]
    [InlineData("relative/path", "10", "500")]
    [InlineData("ftp://127.0.0.1:8104", "10", "500")]
    [InlineData("http://127.0.0.1:8104", "0", "500")]
    [InlineData("http://127.0.0.1:8104", "-1", "500")]
    [InlineData("http://127.0.0.1:8104", "2147483647", "500")]
    [InlineData("http://127.0.0.1:8104", "10", "0")]
    [InlineData("http://127.0.0.1:8104", "10", "-5")]
    public void AddWorkflowAnomalyClient_InvalidConfiguration_RejectsOptions(
        string? baseUrl,
        string timeoutSeconds,
        string maxEvents)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = ConfigurationWith(
            ("WorkflowAnomaly:BaseUrl", baseUrl),
            ("WorkflowAnomaly:TimeoutSeconds", timeoutSeconds),
            ("WorkflowAnomaly:MaxEvents", maxEvents));
        services.AddWorkflowAnomalyClient(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<WorkflowAnomalyOptions>>().Value);
    }

    [Theory]
    [InlineData("http://127.0.0.1:8104/ml?tenant=governance", "query string")]
    [InlineData("http://127.0.0.1:8104/ml?", "query string")]
    [InlineData("http://127.0.0.1:8104/ml#anomaly", "fragment")]
    [InlineData("http://anomaly:secret@127.0.0.1:8104/ml", "username or password")]
    public void AddWorkflowAnomalyClient_UnsafeBaseUrl_RejectsWithActionableError(
        string baseUrl,
        string expectedMessage)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = ConfigurationWith(
            ("WorkflowAnomaly:BaseUrl", baseUrl),
            ("WorkflowAnomaly:TimeoutSeconds", "10"));
        services.AddWorkflowAnomalyClient(configuration);
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<WorkflowAnomalyOptions>>().Value);

        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("http://127.0.0.1:8104", "http://127.0.0.1:8104/workflow-anomaly/predict")]
    [InlineData("http://127.0.0.1:8104/", "http://127.0.0.1:8104/workflow-anomaly/predict")]
    [InlineData("http://127.0.0.1:8104/ml", "http://127.0.0.1:8104/ml/workflow-anomaly/predict")]
    [InlineData("http://127.0.0.1:8104/ml/", "http://127.0.0.1:8104/ml/workflow-anomaly/predict")]
    public async Task AddWorkflowAnomalyClient_ValidBaseUrl_SendsPredictionToPreservedBasePath(
        string baseUrl,
        string expectedRequestUri)
    {
        var handler = new CapturingHttpMessageHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHttpMessageHandlerBuilderFilter>(
            new PrimaryHandlerOverrideFilter(handler));
        var configuration = ConfigurationWith(
            ("WorkflowAnomaly:BaseUrl", baseUrl),
            ("WorkflowAnomaly:TimeoutSeconds", "10"));
        services.AddWorkflowAnomalyClient(configuration);
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IWorkflowAnomalyClient>();

        await client.PredictAnomalyAsync("CASE-URI-TEST", MinimalEvents);

        Assert.Equal(expectedRequestUri, handler.RequestUri?.AbsoluteUri);
    }

    private static IConfiguration ConfigurationWith(params (string Key, string? Value)[] entries)
    {
        var values = entries.ToDictionary(entry => entry.Key, entry => entry.Value);
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }

    private sealed class PrimaryHandlerOverrideFilter : IHttpMessageHandlerBuilderFilter
    {
        private readonly HttpMessageHandler _handler;

        public PrimaryHandlerOverrideFilter(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) =>
            builder =>
            {
                next(builder);
                builder.PrimaryHandler = _handler;
            };
    }
}
