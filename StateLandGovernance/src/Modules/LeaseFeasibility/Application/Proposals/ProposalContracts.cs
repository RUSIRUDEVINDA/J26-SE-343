namespace StateLandGovernance.LeaseFeasibility.Application.Proposals;

public sealed record ProposalField(string Key, string Label);
public sealed record ProposalRequirement(int Number, string Title, string SinhalaTitle, int SourcePage, IReadOnlyList<ProposalField> Fields);
public sealed record ProposalReference(string Id, string Document, string Role, string ProjectType, int PdfPage, int SectionNumber, string Summary)
{
    public IReadOnlyList<int> PdfPages { get; init; } = new[] { PdfPage };
}
public sealed record ProposalGenerationRequest(string ApplicationId, string ProjectName, string Purpose,
    Dictionary<string, string> Facts, ProposalBudget? Budget = null, bool UseAi = false, string Language = "en", ReviewedProposalFinancialInput? ReviewedFinancials = null);
public sealed record ReviewedProposalFinancialInput(decimal? Income, decimal? Balance, decimal? Debt,
    decimal? Lease, decimal? Consistency, int? Overdrafts, string Grade, bool? DefaultHistory, bool Confirmed);
// Projected operating values are applicant estimates, not personal income or approved lease terms.
public sealed record ProposalBudget(decimal? InitialInvestment, decimal? OwnFunding,
    decimal? MonthlySales, decimal? MonthlyProductionCosts, decimal? MonthlyOperatingCosts);
public sealed record ProposalFinancialSummary(decimal? FundingGap, decimal? MonthlyGrossProfit,
    decimal? MonthlyNetProfit, decimal? AnnualSales, decimal? AnnualProductionCosts,
    decimal? AnnualOperatingCosts, decimal? AnnualNetProfit);
public sealed record ProposalSection(int Number, string Title, string Content,
    IReadOnlyList<string> MissingFields, IReadOnlyList<ProposalReference> References, string? SuggestedNarrative = null);
public sealed record ProposalDraft(string ProposalId, string ApplicationId, string ProjectName,
    string TemplateVersion, string GenerationMode, DateTimeOffset GeneratedAt,
    IReadOnlyList<ProposalSection> Sections, ProposalFinancialSummary Financials,
    IReadOnlyList<string> Warnings, string Text);
public interface IProposalReferenceRetriever
{
    IReadOnlyList<ProposalReference> Retrieve(int sectionNumber, string purpose);
}
public interface IProposalNarrativeGenerator
{
    bool IsConfigured { get; }
    Task<IReadOnlyDictionary<int, string>> GenerateAsync(ProposalGenerationRequest request,
        IReadOnlyList<ProposalSection> sections, CancellationToken cancellationToken);
}
