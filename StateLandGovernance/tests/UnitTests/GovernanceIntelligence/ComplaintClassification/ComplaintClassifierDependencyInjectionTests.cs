using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.ComplaintClassification;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence.ComplaintClassification;

public sealed class ComplaintClassifierDependencyInjectionTests
{
    [Fact]
    public void AddComplaintClassifier_ValidConfiguration_ResolvesTypedApplicationClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = ConfigurationWith(
            ("ComplaintClassifier:BaseUrl", "http://127.0.0.1:8104"),
            ("ComplaintClassifier:TimeoutSeconds", "7"));

        services.AddComplaintClassifier(configuration);
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var applicationClient = provider.GetRequiredService<IComplaintClassificationClient>();
        var options = provider.GetRequiredService<IOptions<ComplaintClassifierOptions>>().Value;

        Assert.IsType<HttpComplaintClassificationClient>(applicationClient);
        Assert.Equal("http://127.0.0.1:8104", options.BaseUrl);
        Assert.Equal(7, options.TimeoutSeconds);
    }

    [Theory]
    [InlineData(null, "10")]
    [InlineData("relative/path", "10")]
    [InlineData("ftp://127.0.0.1:8104", "10")]
    [InlineData("http://127.0.0.1:8104", "0")]
    [InlineData("http://127.0.0.1:8104", "-1")]
    [InlineData("http://127.0.0.1:8104", "2147483647")]
    public void AddComplaintClassifier_InvalidConfiguration_RejectsOptions(
        string? baseUrl,
        string timeoutSeconds)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = ConfigurationWith(
            ("ComplaintClassifier:BaseUrl", baseUrl),
            ("ComplaintClassifier:TimeoutSeconds", timeoutSeconds));
        services.AddComplaintClassifier(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ComplaintClassifierOptions>>().Value);
    }

    [Theory]
    [InlineData("http://127.0.0.1:8104/ml?tenant=governance", "query string")]
    [InlineData("http://127.0.0.1:8104/ml?", "query string")]
    [InlineData("http://127.0.0.1:8104/ml#classifier", "fragment")]
    [InlineData("http://classifier:secret@127.0.0.1:8104/ml", "username or password")]
    public void AddComplaintClassifier_UnsafeBaseUrl_RejectsWithActionableError(
        string baseUrl,
        string expectedMessage)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = ConfigurationWith(
            ("ComplaintClassifier:BaseUrl", baseUrl),
            ("ComplaintClassifier:TimeoutSeconds", "10"));
        services.AddComplaintClassifier(configuration);
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ComplaintClassifierOptions>>().Value);

        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("http://127.0.0.1:8104", "http://127.0.0.1:8104/predict")]
    [InlineData("http://127.0.0.1:8104/", "http://127.0.0.1:8104/predict")]
    [InlineData("http://127.0.0.1:8104/ml", "http://127.0.0.1:8104/ml/predict")]
    [InlineData("http://127.0.0.1:8104/ml/", "http://127.0.0.1:8104/ml/predict")]
    public async Task AddComplaintClassifier_ValidBaseUrl_SendsPredictionToPreservedBasePath(
        string baseUrl,
        string expectedRequestUri)
    {
        var handler = new CapturingHttpMessageHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHttpMessageHandlerBuilderFilter>(
            new PrimaryHandlerOverrideFilter(handler));
        var configuration = ConfigurationWith(
            ("ComplaintClassifier:BaseUrl", baseUrl),
            ("ComplaintClassifier:TimeoutSeconds", "10"));
        services.AddComplaintClassifier(configuration);
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IComplaintClassificationClient>();

        await client.ClassifyAsync("A fictional complaint for URI verification.");

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
