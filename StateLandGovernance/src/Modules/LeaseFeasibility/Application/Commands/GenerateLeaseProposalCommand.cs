using System.Globalization;
using System.Text;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;
using StateLandGovernance.LeaseFeasibility.Domain.Exceptions;
using StateLandGovernance.LeaseFeasibility.Domain.Services;
using StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;
using StateLandGovernance.LeaseFeasibility.Application.Proposals;

namespace StateLandGovernance.LeaseFeasibility.Application.Commands;

public sealed record GenerateLeaseProposalCommand(ProposalGenerationRequest Input);

public sealed class GenerateLeaseProposalCommandHandler(
    IProposalReferenceRetriever references, IProposalNarrativeGenerator narratives, TimeProvider timeProvider)
{
    public async Task<ProposalDraft> HandleAsync(GenerateLeaseProposalCommand command, CancellationToken cancellationToken = default)
    {
        var input = command.Input;
        Validate(input);
        var reviewedEvidence = ReviewedEvidence(input.ReviewedFinancials);
        if (input.UseAi && !narratives.IsConfigured)
            throw new InvalidOperationException("AI drafting is not configured. Use structured drafting or configure the server model settings.");
        var facts = input.Facts;
        var sections = LandDepartmentProposalTemplate.Sections.Select(requirement =>
        {
            var missing = requirement.Fields.Where(f => !facts.TryGetValue(f.Key, out var value) || string.IsNullOrWhiteSpace(value)).Select(f => f.Label).ToList();
            var text = string.Join("\n", requirement.Fields.Select(f => $"{f.Label}: {(facts.TryGetValue(f.Key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : "[TO BE CONFIRMED]")}"));
            if (requirement.Number == 2) text = $"Project: {input.ProjectName.Trim()}\nPurpose: {input.Purpose.Trim()}\n" + text;
            var source = new ProposalReference($"department-{requirement.Number}", "minimum requirements proposal.pdf", "requirement", "all", requirement.SourcePage, requirement.Number,
                "Required fields: " + string.Join("; ", requirement.Fields.Select(f => f.Label)))
                { PdfPages = requirement.Number == 2 ? new[] { 1, 2, 3, 4, 5 } : new[] { requirement.SourcePage } };
            return new ProposalSection(requirement.Number, input.Language == "si" ? requirement.SinhalaTitle : requirement.Title, text, missing,
                new[] { source }.Concat(references.Retrieve(requirement.Number, input.Purpose.ToLowerInvariant())).ToArray());
        }).ToArray();
        var budget = input.Budget;
        var financials = Calculate(budget);
        AddFinancial(7, "Applicant-estimated monthly sales", budget?.MonthlySales, "Annual sales assuming 12 identical operating months", financials.AnnualSales);
        AddFinancial(8, "Applicant-estimated monthly production costs", budget?.MonthlyProductionCosts, "Applicant-estimated monthly operating costs", budget?.MonthlyOperatingCosts);
        AddFinancial(8, "Monthly working capital subtotal (production + operating costs)", budget?.MonthlyProductionCosts + budget?.MonthlyOperatingCosts);
        AddFinancial(9, "Initial investment", budget?.InitialInvestment, "Planned own funding", budget?.OwnFunding);
        AddFinancial(9, "Remaining funding requirement", financials.FundingGap);
        AddFinancial(11, "Monthly gross profit", financials.MonthlyGrossProfit, "Monthly net profit", financials.MonthlyNetProfit);
        AddFinancial(11, "Annual production costs (12 equal months)", financials.AnnualProductionCosts, "Annual operating costs (12 equal months)", financials.AnnualOperatingCosts);
        AddFinancial(11, "Annual net profit (12 equal months)", financials.AnnualNetProfit);
        var warnings = new List<string> {
            "Draft for human review, not an approval decision or approval probability.",
            "Example proposal approval outcomes are unverified. Sources are reviewed summaries, not verbatim OCR.",
            "Project budgets are applicant estimates. They do not establish verified personal income or lease affordability.",
            "Annual projections assume 12 identical operating months; revise for harvest delays, cycles, seasonality and startup periods.",
            "Attachment references are statements supplied by the applicant; this generator does not verify attached survey plans or permissions.",
            "Drafts are not stored on the server. Export before leaving the session."
        };
        if (sections.Any(s => s.MissingFields.Count > 0)) warnings.Add("Required information is missing. Complete every flagged field before submission.");
        if (input.UseAi)
        {
            var suggestions = await narratives.GenerateAsync(input, sections, cancellationToken);
            sections = sections.Select(s => suggestions.TryGetValue(s.Number, out var narrative)
                ? s with { SuggestedNarrative = narrative } : s).ToArray();
            warnings.Add("AI narrative suggestions are unverified. Review them against the applicant facts and cited references.");
        }
        // Append personal financial evidence after the model call; it never enters model context.
        if (reviewedEvidence != null) sections[10] = sections[10] with { Content = sections[10].Content + "\n\n" + reviewedEvidence };
        else warnings.Add("No user-confirmed financial assessment was included in this proposal.");
        var id = Guid.NewGuid().ToString("N");
        var now = timeProvider.GetUtcNow();
        var body = new StringBuilder($"{input.ProjectName.Trim()}\nPROJECT PROPOSAL - DRAFT FOR REVIEW\nApplication: {input.ApplicationId.Trim()}\nTemplate: {LandDepartmentProposalTemplate.Version}\nGenerated: {now:O}\n\n");
        foreach (var section in sections)
        {
            body.AppendLine($"{section.Number:00}. {section.Title}").AppendLine(section.Content);
            if (section.SuggestedNarrative != null) body.AppendLine("AI narrative suggestion - requires verification:").AppendLine(section.SuggestedNarrative);
            body.AppendLine("Required information still missing: " + (section.MissingFields.Count == 0 ? "None identified by field-presence check; content still requires review." : string.Join("; ", section.MissingFields)));
            foreach (var reference in section.References) body.AppendLine($"Source [{reference.Id}]: {reference.Document}, PDF pages {string.Join(", ", reference.PdfPages)} ({reference.Role}). {reference.Summary}");
            body.AppendLine();
        }
        body.AppendLine("REVIEW NOTES");
        foreach (var warning in warnings) body.AppendLine("- " + warning);
        return new ProposalDraft(id, input.ApplicationId.Trim(), input.ProjectName.Trim(), LandDepartmentProposalTemplate.Version,
            input.UseAi ? "retrieval-augmented-ai" : "source-linked-structured-draft", now, sections, financials, warnings, body.ToString());

        void AddFinancial(int number, string label, decimal? value, string? secondLabel = null, decimal? secondValue = null)
        {
            var i = number - 1;
            var missing = sections[i].MissingFields.ToList();
            if (!value.HasValue) missing.Add(label);
            if (secondLabel != null && !secondValue.HasValue) missing.Add(secondLabel);
            var content = sections[i].Content + $"\n{label}: {Amount(value)}";
            if (secondLabel != null) content += $"\n{secondLabel}: {Amount(secondValue)}";
            sections[i] = sections[i] with { Content = content, MissingFields = missing };
        }
    }
    private static string? ReviewedEvidence(ReviewedProposalFinancialInput? input)
    {
        if (input == null) return null;
        if (!input.Confirmed || input.Income == null || input.Balance == null || input.Debt == null || input.Lease == null ||
            input.Consistency == null || input.Overdrafts == null || input.DefaultHistory == null ||
            !Enum.TryParse<CreditRiskGrade>(input.Grade, out var grade) || !Enum.IsDefined(grade))
            throw new ArgumentException("Confirm every financial assessment input. Missing evidence cannot be treated as zero or false.");
        try
        {
            var result = new FinancialFeasibilityScoringEngine().EvaluateFeasibility(new FinancialFeasibilityScoringInput(
                "proposal-review", "proposal-applicant", input.Income.Value, input.Consistency.Value, input.Lease.Value,
                input.Debt.Value, input.Balance.Value, input.Overdrafts.Value, grade, input.DefaultHistory.Value), DateTimeOffset.UtcNow);
            return $"User-confirmed financial assessment inputs (separate from projected farm revenue):\n" +
                $"Monthly income: {Amount(input.Income)}\nExisting monthly debt repayments: {Amount(input.Debt)}\nRequested monthly lease payment: {Amount(input.Lease)}\n" +
                $"Financial grade: {result.Grade}; assessment action: {result.Action}. This is financial decision support, not government approval.";
        }
        catch (InvalidFinancialProfileException ex) { throw new ArgumentException(ex.Message); }
        catch (OverflowException) { throw new ArgumentException("Financial assessment inputs exceed the supported range."); }
    }
    private static string Amount(decimal? value) => value.HasValue ? "LKR " + value.Value.ToString("N2", CultureInfo.InvariantCulture) : "[TO BE CONFIRMED]";
    public static ProposalFinancialSummary Calculate(ProposalBudget? budget) => new(
        budget?.InitialInvestment.HasValue == true && budget.OwnFunding.HasValue ? Math.Max(0, budget.InitialInvestment.Value - budget.OwnFunding.Value) : null,
        budget?.MonthlySales - budget?.MonthlyProductionCosts,
        budget?.MonthlySales - budget?.MonthlyProductionCosts - budget?.MonthlyOperatingCosts,
        budget?.MonthlySales * 12, budget?.MonthlyProductionCosts * 12, budget?.MonthlyOperatingCosts * 12,
        (budget?.MonthlySales - budget?.MonthlyProductionCosts - budget?.MonthlyOperatingCosts) * 12);
    private static void Validate(ProposalGenerationRequest input)
    {
        if (string.IsNullOrWhiteSpace(input.ApplicationId) || input.ApplicationId.Length > 120 ||
            string.IsNullOrWhiteSpace(input.ProjectName) || input.ProjectName.Length > 200 ||
            string.IsNullOrWhiteSpace(input.Purpose) || input.Purpose.Length > 2000)
            throw new ArgumentException("Provide an application reference (max 120 characters), project name (max 200) and purpose (max 2000).");
        if (input.Language is not ("en" or "si")) throw new ArgumentException("Language must be en or si.");
        var allowed = LandDepartmentProposalTemplate.Sections.SelectMany(s => s.Fields).Select(f => f.Key).ToHashSet();
        if (input.Facts == null || input.Facts.Count > allowed.Count || input.Facts.Any(f => !allowed.Contains(f.Key) || f.Value == null || f.Value.Length > 4000))
            throw new ArgumentException("Use known proposal fields with at most 4000 characters each.");
        if (input.Budget != null && new[] { input.Budget.InitialInvestment, input.Budget.OwnFunding, input.Budget.MonthlySales,
            input.Budget.MonthlyProductionCosts, input.Budget.MonthlyOperatingCosts }.Any(v => v is < 0 or > 1_000_000_000_000m))
            throw new ArgumentException("Budget estimates must be between zero and LKR 1 trillion. Leave unknown amounts blank.");
    }
}
