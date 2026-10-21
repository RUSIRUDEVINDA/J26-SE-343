using System.Text.Json;
using StateLandGovernance.LeaseFeasibility.Application.Proposals;

namespace StateLandGovernance.LeaseFeasibility.Infrastructure.Proposals;

// Section-filtered retrieval over a small reviewed corpus. No embeddings or approval claims.
public sealed class ReviewedProposalReferenceRetriever : IProposalReferenceRetriever
{
    private readonly IReadOnlyList<ProposalReference> _references;
    public ReviewedProposalReferenceRetriever()
    {
        using var stream = typeof(ReviewedProposalReferenceRetriever).Assembly.GetManifestResourceStream(
            "StateLandGovernance.LeaseFeasibility.Infrastructure.Proposals.reference-sections.json")
            ?? throw new InvalidOperationException("Proposal reference corpus is missing.");
        _references = JsonSerializer.Deserialize<List<ProposalReference>>(stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidOperationException("Invalid proposal corpus.");
    }
    public IReadOnlyList<ProposalReference> Retrieve(int sectionNumber, string purpose)
    {
        var poultry = new[] { "poultry", "broiler", "chicken", "කුකුළු", "කුකුල්" }.Any(purpose.Contains);
        var coconut = new[] { "coconut", "පොල්" }.Any(purpose.Contains);
        return _references.Where(r => r.SectionNumber == sectionNumber &&
            (!poultry && !coconut || r.ProjectType == (poultry ? "poultry" : "coconut")))
            .OrderBy(r => r.Id, StringComparer.Ordinal).Take(2).ToArray();
    }
}
