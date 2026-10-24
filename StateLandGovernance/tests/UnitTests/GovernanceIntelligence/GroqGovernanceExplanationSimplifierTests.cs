using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.Explainability;
using StateLandGovernance.BuildingBlocks.Observability;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public sealed class GroqGovernanceExplanationSimplifierTests
{
    [Fact]
    public async Task SimplifyAsync_ValidHostedResponse_ReturnsValidatedPlainLanguageTextWithoutSensitivePayload()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHandler(async request =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            const string response = "{\"choices\":[{\"message\":{\"content\":\"{\\\"explanations\\\":[{\\\"itemId\\\":\\\"EXP-1\\\",\\\"plainLanguageExplanation\\\":\\\"The case needs review before it can proceed.\\\"}]}\"}}]}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        });
        var metrics = new PrometheusMetricsRegistry();
        var sut = new GroqGovernanceExplanationSimplifier(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.groq.com/openai/v1/") },
            Options.Create(new GroqExplainabilityOptions { Enabled = true, Model = "qwen/qwen3.8-27b", Token = "test-token" }),
            NullLogger<GroqGovernanceExplanationSimplifier>.Instance,
            metrics);
        var request = new GovernanceExplanationSimplificationRequest(new[]
        {
            new GovernanceExplanationSimplificationItem("EXP-1", "Sensitive applicant title", "NonCompliant", "High", "RULE-1", new[] { "NIC 123456789V" }, "Contact Jane Doe")
        });

        var result = await sut.SimplifyAsync(request);

        Assert.True(result.Applied);
        Assert.Equal("Groq", result.Provider);
        Assert.Equal(GroqGovernanceExplanationSimplifier.PromptVersion, result.PromptVersion);
        Assert.Equal("The case needs review before it can proceed.", result.PlainLanguageExplanations["EXP-1"]);
        Assert.Equal("Bearer", capturedRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("test-token", capturedRequest.Headers.Authorization.Parameter);
        Assert.DoesNotContain("Sensitive applicant title", capturedBody);
        Assert.DoesNotContain("123456789V", capturedBody);
        Assert.DoesNotContain("Contact Jane Doe", capturedBody);
        var renderedMetrics = metrics.Render();
        Assert.Contains("provider=\"Groq\"", renderedMetrics);
        Assert.Contains("status=\"Applied\"", renderedMetrics);
        Assert.Contains("applied=\"true\"", renderedMetrics);
    }

    [Fact]
    public async Task SimplifyAsync_MalformedHostedResponse_FailsClosedToDeterministicFallback()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"not-json\"}}]}", Encoding.UTF8, "application/json")
        }));
        var sut = new GroqGovernanceExplanationSimplifier(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.groq.com/openai/v1/") },
            Options.Create(new GroqExplainabilityOptions { Enabled = true, Model = "qwen/qwen3.8-27b", Token = "test-token" }),
            NullLogger<GroqGovernanceExplanationSimplifier>.Instance);

        var result = await sut.SimplifyAsync(new GovernanceExplanationSimplificationRequest(new[]
        {
            new GovernanceExplanationSimplificationItem("EXP-1", "Title", "NonCompliant", "High", "RULE-1", Array.Empty<string>(), "Review")
        }));

        Assert.False(result.Applied);
        Assert.Equal("InvalidResponse", result.Status);
        Assert.Empty(result.PlainLanguageExplanations);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responseFactory;
        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) => _responseFactory = responseFactory;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _responseFactory(request);
    }
}
