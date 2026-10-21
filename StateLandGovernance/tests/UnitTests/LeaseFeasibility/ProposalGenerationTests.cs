using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StateLandGovernance.LeaseFeasibility.Application.Commands;
using StateLandGovernance.LeaseFeasibility.Application.Proposals;
using StateLandGovernance.LeaseFeasibility.Infrastructure.Proposals;

namespace StateLandGovernance.UnitTests.LeaseFeasibility;

public sealed class ProposalGenerationTests
{
    private static ProposalGenerationRequest Input() => new("APP-TEST", "Test farm", "coconut cultivation", new());
    private static GenerateLeaseProposalCommandHandler Handler(IProposalNarrativeGenerator? ai = null) => new(
        new ReviewedProposalReferenceRetriever(), ai ?? new DisabledAi(), TimeProvider.System);
    [Fact]
    public async Task GeneratesAllRequirementsAndKeepsUnknownValuesUnknown()
    {
        var draft = await Handler().HandleAsync(new(Input()));
        Assert.Equal(Enumerable.Range(1, 14), draft.Sections.Select(s => s.Number));
        Assert.Null(draft.Financials.MonthlyNetProfit);
        Assert.Contains("[TO BE CONFIRMED]", draft.Text);
        Assert.All(draft.Sections, section => Assert.Contains(section.References, r => r.Role == "requirement"));
        Assert.Contains(draft.Sections[1].MissingFields, field => field.Contains("15-year"));
        Assert.Equal("source-linked-structured-draft", draft.GenerationMode);
    }
    [Fact]
    public async Task PreservesApplicantStatementsAndDoesNotCopyExampleFacts()
    {
        var request = Input() with { Facts = new() { ["applicantName"] = "My applicant", ["production"] = "Estimate pending survey" } };
        var draft = await Handler().HandleAsync(new(request));
        Assert.Contains("My applicant", draft.Sections[0].Content);
        Assert.DoesNotContain("Applicant name", draft.Sections[0].MissingFields);
        Assert.Contains("Estimate pending survey", draft.Sections[9].Content);
        Assert.DoesNotContain("52,820,000", draft.Text);
    }
    [Fact]
    public void RetrievesMatchingProjectReferencesWithoutApprovalClaims()
    {
        var references = new ReviewedProposalReferenceRetriever().Retrieve(6, "broiler poultry");
        Assert.Single(references);
        Assert.Equal("Leo farm proposal.pdf", references[0].Document);
        Assert.Equal(14, references[0].PdfPage);
        Assert.Equal("example-unverified-outcome", references[0].Role);
    }
    [Fact]
    public void CalculatesBudgetAndRetainsNegativeProfit()
    {
        var result = GenerateLeaseProposalCommandHandler.Calculate(new(100000, 40000, 10000, 9000, 3000));
        Assert.Equal(60000, result.FundingGap);
        Assert.Equal(1000, result.MonthlyGrossProfit);
        Assert.Equal(-2000, result.MonthlyNetProfit);
        Assert.Equal(-24000, result.AnnualNetProfit);
    }
    [Fact]
    public void MissingCostsAreNotZeroAndFundingGapCannotBeNegative()
    {
        var result = GenerateLeaseProposalCommandHandler.Calculate(new(100, 200, 500, null, 10));
        Assert.Equal(0, result.FundingGap);
        Assert.Null(result.MonthlyNetProfit);
        Assert.Null(result.AnnualNetProfit);
    }
    [Fact]
    public async Task RejectsNegativeBudgetAndUnknownFactKeys()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Handler().HandleAsync(new(Input() with { Budget = new(-1, null, null, null, null) })));
        await Assert.ThrowsAsync<ArgumentException>(() => Handler().HandleAsync(new(Input() with { Facts = new() { ["invented"] = "x" } })));
    }
    [Fact]
    public async Task ExplicitAiRequestCannotSilentlyFallBack()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().HandleAsync(new(Input() with { UseAi = true })));
    }
    [Fact]
    public async Task AiSuggestionsDoNotOverwriteFactsAndHaveDistinctMode()
    {
        var draft = await Handler(new FakeAi()).HandleAsync(new(Input() with { UseAi = true, Facts = new() { ["production"] = "Applicant estimate" } }));
        Assert.Equal("retrieval-augmented-ai", draft.GenerationMode);
        Assert.Contains("Applicant estimate", draft.Sections[9].Content);
        Assert.Equal("Review the production assumptions [department-10].", draft.Sections[9].SuggestedNarrative);
    }
    [Fact]
    public async Task ModelRequestExcludesIdentityAndDoesNotStoreResponse()
    {
        var http = new ModelHttp();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ProposalGeneration:ApiKey"] = "fake-test-key", ["ProposalGeneration:Model"] = "test-model"
        }).Build();
        var draft = await Handler(new OpenAiProposalNarrativeGenerator(new HttpClient(http), configuration)).HandleAsync(
            new(Input() with { UseAi = true, Facts = new() { ["nic"] = "PRIVATE-ID-123", ["applicantName"] = "PRIVATE-NAME" } }));
        Assert.DoesNotContain("PRIVATE-ID-123", http.Body);
        Assert.DoesNotContain("PRIVATE-NAME", http.Body);
        using var body = JsonDocument.Parse(http.Body);
        Assert.False(body.RootElement.GetProperty("store").GetBoolean());
        Assert.Equal(13, draft.Sections.Count(s => s.SuggestedNarrative != null));
    }
    [Fact]
    public async Task RejectsIncompleteAiOutput()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ProposalGeneration:ApiKey"] = "fake-test-key", ["ProposalGeneration:Model"] = "test-model"
        }).Build();
        var ai = new OpenAiProposalNarrativeGenerator(new HttpClient(new ModelHttp { Status = "incomplete" }), config);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler(ai).HandleAsync(new(Input() with { UseAi = true })));
    }
    [Fact]
    public async Task RecalculatesReviewedFinancialsSeparatelyFromProjectRevenue()
    {
        var input = Input() with { ReviewedFinancials = new(200000, 100000, 20000, 10000, 1, 0, "A", false, true),
            Budget = new(1000000, 500000, 800000, 500000, 100000) };
        var draft = await Handler().HandleAsync(new(input));
        Assert.Equal(200000, draft.Financials.MonthlyNetProfit);
        Assert.Contains("Monthly income: LKR 200,000.00", draft.Sections[10].Content);
        Assert.Contains("separate from projected farm revenue", draft.Sections[10].Content);
    }
    [Fact]
    public async Task RejectsUnconfirmedOrIncompletePersonalFinancialEvidence()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Handler().HandleAsync(new(Input() with {
            ReviewedFinancials = new(200000, 100000, null, 10000, 1, 0, "A", false, true) })));
        await Assert.ThrowsAsync<ArgumentException>(() => Handler().HandleAsync(new(Input() with {
            ReviewedFinancials = new(200000, 100000, 20000, 10000, 1, 0, "A", false, false) })));
    }
    private sealed class DisabledAi : IProposalNarrativeGenerator
    {
        public bool IsConfigured => false;
        public Task<IReadOnlyDictionary<int, string>> GenerateAsync(ProposalGenerationRequest request, IReadOnlyList<ProposalSection> sections, CancellationToken cancellationToken) => throw new Exception("Must not call unconfigured model");
    }
    private sealed class FakeAi : IProposalNarrativeGenerator
    {
        public bool IsConfigured => true;
        public Task<IReadOnlyDictionary<int, string>> GenerateAsync(ProposalGenerationRequest request, IReadOnlyList<ProposalSection> sections, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string> { [10] = "Review the production assumptions [department-10]." });
    }
    private sealed class ModelHttp : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        public string Status { get; init; } = "completed";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var content = JsonSerializer.Serialize(new { sections = Enumerable.Range(2, 13).Select(n => new { number = n, narrative = $"Review [department-{n}]." }) });
            var response = JsonSerializer.Serialize(new { status = Status, output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = content } } } } });
            return new(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
