namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

/// <summary>Reads an exact template version; never derives requirements from extraction fields.</summary>
public sealed class PostgresProposalTemplateProvider(WorkflowGovernanceDbContext db) : IProposalTemplateProvider
{
    public async Task<ProposalTemplate?> GetTemplateAsync(ProposalTemplateId id, string version,
        CancellationToken cancellationToken = default)
    {
        var row = await db.CompletenessDefinitions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Kind == "Proposal" && x.Identifier == id.Value && x.Version == version.Trim(), cancellationToken);
        if (row is null) return null;
        var definition = PersistenceJsonSerializer.Deserialize<ProposalTemplateDefinition>(row.DefinitionJson)
            ?? throw new InvalidOperationException("Stored proposal definition is empty.");
        return new ProposalTemplate(id, row.Version, definition.Name, row.SourceReference,
            definition.Status, definition.Requirements, definition.EffectivePeriod);
    }
}

public sealed record ProposalTemplateDefinition(string Name, ProposalTemplateStatus Status,
    List<ProposalContentRequirement> Requirements, ProposalTemplateEffectivePeriod? EffectivePeriod = null);
