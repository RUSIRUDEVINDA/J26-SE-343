namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

/// <summary>Reads exact approved configuration versions from PostgreSQL. No implicit seeds.</summary>
public sealed class PostgresDocumentRequirementProvider(WorkflowGovernanceDbContext db) : IDocumentRequirementProvider
{
    public async Task<IReadOnlyList<DocumentRequirementSnapshot>?> GetRequirementSetAsync(
        string requirementSetIdentifier, string requirementSetVersion, CancellationToken cancellationToken = default)
    {
        var row = await db.CompletenessDefinitions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Kind == "Documents" && x.Identifier == requirementSetIdentifier.Trim()
            && x.Version == requirementSetVersion.Trim(), cancellationToken);
        return row is null ? null : PersistenceJsonSerializer.Deserialize<List<DocumentRequirementSnapshot>>(row.DefinitionJson);
    }
}
