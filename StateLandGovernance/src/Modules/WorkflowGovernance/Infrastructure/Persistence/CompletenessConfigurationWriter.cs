namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;

using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

/// <summary>Explicit provisioning adapter. Call only with an approved, traceable configuration;
/// extraction catalogs and development fixtures are not approval sources.</summary>
public sealed class CompletenessConfigurationWriter(WorkflowGovernanceDbContext db)
{
    public Task PublishDocumentsAsync(string identifier, string version, string sourceReference,
        Guid approvedBy, IReadOnlyList<DocumentRequirementSnapshot> requirements, CancellationToken ct = default)
    {
        if (requirements.Count == 0 || requirements.Select(x => x.Id).Distinct().Count() != requirements.Count)
            throw new ArgumentException("A nonempty requirement set with unique identifiers is required.");
        return PublishAsync("Documents", identifier, version, sourceReference, approvedBy,
            PersistenceJsonSerializer.Serialize(requirements), ct);
    }

    public Task PublishTemplateAsync(ProposalTemplate template, Guid approvedBy, CancellationToken ct = default)
        => PublishAsync("Proposal", template.Id.Value, template.Version, template.AuthoritativeSourceReference,
            approvedBy, PersistenceJsonSerializer.Serialize(new ProposalTemplateDefinition(template.Name,
                template.Status, template.Requirements.ToList(), template.EffectivePeriod)), ct);

    private async Task PublishAsync(string kind, string identifier, string version, string source,
        Guid approvedBy, string json, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (approvedBy == Guid.Empty) throw new ArgumentException("Approving actor is required.");
        db.CompletenessDefinitions.Add(new CompletenessDefinitionEntity { Kind = kind,
            Identifier = identifier.Trim(), Version = version.Trim(), SourceReference = source.Trim(),
            ApprovedByActorId = approvedBy, DefinitionJson = json });
        // Composite PK rejects replacement of an existing version, including concurrent publication.
        await db.SaveChangesAsync(ct);
    }
}
