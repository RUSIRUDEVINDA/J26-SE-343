namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

internal static class GovernedDocumentPersistenceMapper
{
    public static GovernedDocumentEntity ToPersistence(GovernedDocument domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        var firstVersion = domain.Versions.OrderBy(v => v.VersionNumber).FirstOrDefault();
        var createdAt = firstVersion?.SubmittedAt ?? DateTime.UtcNow;

        var entity = new GovernedDocumentEntity
        {
            Id = domain.Id.Value,
            LeaseCaseId = domain.LeaseCaseId.Value,
            LogicalCategory = domain.LogicalCategory,
            ActiveVersionId = domain.ActiveVersionId.Value,
            Revision = domain.Revision,
            CreatedAt = createdAt.ToUniversalTime(),
            UpdatedAt = DateTime.UtcNow
        };

        entity.Versions = domain.Versions.Select(v => new DocumentVersionEntity
        {
            Id = v.Id.Value,
            GovernedDocumentId = domain.Id.Value,
            VersionNumber = v.VersionNumber,
            PredecessorVersionId = v.PredecessorVersionId?.Value,
            ChecksumAlgorithm = v.Checksum.Algorithm,
            ChecksumValue = v.Checksum.Value,
            StorageKind = "LocalFile",
            StoragePath = v.ContentReference.Value,
            OriginalFileName = v.OriginalFileName,
            MediaType = v.MediaType,
            FileSizeInBytes = v.FileSizeInBytes,
            SubmittedByActorId = v.SubmittedByActorId,
            SubmittedAt = v.SubmittedAt.ToUniversalTime(),
            State = "Submitted"
        }).ToList();

        return entity;
    }

    public static GovernedDocument ToDomain(GovernedDocumentEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var domain = AggregateRehydrator.Create<GovernedDocument>();

        AggregateRehydrator.SetPropertyBackingField(domain, nameof(GovernedDocument.Id), new GovernedDocumentId(entity.Id));
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(GovernedDocument.LeaseCaseId), new LeaseCaseId(entity.LeaseCaseId));
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(GovernedDocument.LogicalCategory), entity.LogicalCategory);
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(GovernedDocument.ActiveVersionId), new DocumentVersionId(entity.ActiveVersionId));
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(GovernedDocument.Revision), entity.Revision);

        var versions = new List<DocumentVersion>();
        foreach (var v in entity.Versions.OrderBy(x => x.VersionNumber))
        {
            var version = AggregateRehydrator.Create<DocumentVersion>();
            AggregateRehydrator.SetPropertyBackingField(version, nameof(DocumentVersion.Id), new DocumentVersionId(v.Id));
            AggregateRehydrator.SetPropertyBackingField(version, nameof(DocumentVersion.VersionNumber), v.VersionNumber);
            AggregateRehydrator.SetPropertyBackingField(
                version,
                nameof(DocumentVersion.PredecessorVersionId),
                v.PredecessorVersionId.HasValue ? new DocumentVersionId(v.PredecessorVersionId.Value) : (DocumentVersionId?)null);
            AggregateRehydrator.SetPropertyBackingField(
                version,
                nameof(DocumentVersion.Checksum),
                new DocumentChecksum(v.ChecksumAlgorithm, v.ChecksumValue));
            AggregateRehydrator.SetPropertyBackingField(
                version,
                nameof(DocumentVersion.ContentReference),
                new DocumentContentReference(v.StoragePath));
            AggregateRehydrator.SetPropertyBackingField(version, nameof(DocumentVersion.OriginalFileName), v.OriginalFileName);
            AggregateRehydrator.SetPropertyBackingField(version, nameof(DocumentVersion.MediaType), v.MediaType);
            AggregateRehydrator.SetPropertyBackingField(version, nameof(DocumentVersion.FileSizeInBytes), v.FileSizeInBytes);
            AggregateRehydrator.SetPropertyBackingField(version, nameof(DocumentVersion.SubmittedByActorId), v.SubmittedByActorId);
            AggregateRehydrator.SetPropertyBackingField(version, nameof(DocumentVersion.SubmittedAt), DateTime.SpecifyKind(v.SubmittedAt, DateTimeKind.Utc));

            versions.Add(version);
        }

        AggregateRehydrator.SetField(domain, "_versions", versions);
        AggregateRehydrator.SetField(domain, "_domainEvents", new List<IDomainEvent>());

        return domain;
    }
}
