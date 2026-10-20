namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

internal static class DocumentAnalysisPersistenceMapper
{
    public static DocumentAnalysisEntity ToPersistence(DocumentAnalysis domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        var entity = new DocumentAnalysisEntity
        {
            Id = domain.Id.Value,
            GovernedDocumentId = domain.GovernedDocumentId.Value,
            DocumentVersionId = domain.DocumentVersionId.Value,
            ChecksumAlgorithm = domain.DocumentChecksum.Algorithm,
            ChecksumValue = domain.DocumentChecksum.Value,
            DocumentVersionNumber = domain.DocumentVersionNumber,
            SourceDocumentRevision = domain.SourceDocumentRevision,
            CreatedAt = domain.CreatedAt.ToUniversalTime(),
            Revision = domain.Revision,
            UpdatedAt = DateTime.UtcNow
        };

        entity.Runs = domain.Runs.Select(r =>
        {
            var runEntity = new AnalysisRunEntity
            {
                Id = r.Id.Value,
                DocumentAnalysisId = domain.Id.Value,
                RunNumber = r.RunNumber,
                DocumentVersionId = r.DocumentVersionId.Value,
                ChecksumAlgorithm = r.DocumentChecksum.Algorithm,
                ChecksumValue = r.DocumentChecksum.Value,
                ModelProvider = r.ModelReference.Provider,
                ModelName = r.ModelReference.ModelName,
                ModelVersion = r.ModelReference.ModelVersion,
                RequestedCapabilitiesJson = PersistenceJsonSerializer.Serialize(r.RequestedCapabilities.Select(c => c.Value).ToList()),
                RequestedAt = r.RequestedAt.ToUniversalTime(),
                State = r.State.ToString(),
                StartedAt = r.StartedAt?.ToUniversalTime(),
                CompletedAt = r.CompletedAt?.ToUniversalTime(),
                FailedAt = r.FailedAt?.ToUniversalTime(),
                SupersededAt = r.SupersededAt?.ToUniversalTime(),
                SupersessionReason = r.SupersessionReason,
                FailureJson = r.Failure is null ? null : PersistenceJsonSerializer.Serialize(new AnalysisRunFailureDto(r.Failure.Code, r.Failure.Description)),
                ResultJson = r.Result is null ? null : SerializeAnalysisRunResult(r.Result)
            };
            return runEntity;
        }).ToList();

        entity.Verifications = domain.Verifications.Select(v =>
        {
            var dto = new HumanFactVerificationDto
            {
                Id = v.Id.Value,
                DocumentAnalysisId = v.DocumentAnalysisId.Value,
                AnalysisRunId = v.AnalysisRunId.Value,
                AnalysisRunResultId = v.AnalysisRunResultId.Value,
                ExtractedFactId = v.ExtractedFactId.Value,
                DocumentVersionId = v.DocumentVersionId.Value,
                ChecksumAlgorithm = v.DocumentChecksum.Algorithm,
                ChecksumValue = v.DocumentChecksum.Value,
                RunNumber = v.RunNumber,
                ModelProvider = v.ModelReference.Provider,
                ModelName = v.ModelReference.ModelName,
                ModelVersion = v.ModelReference.ModelVersion,
                FactCode = v.FactCode.Value,
                OriginalValueKind = v.OriginalValue.Kind.ToString(),
                OriginalCanonicalValue = v.OriginalValue.CanonicalValue,
                Decision = v.Decision.ToString(),
                CorrectedValueKind = v.CorrectedValue?.Kind.ToString(),
                CorrectedCanonicalValue = v.CorrectedValue?.CanonicalValue,
                Reason = v.Reason,
                VerifiedAt = v.VerifiedAt.ToUniversalTime(),
                VerifyingActorId = v.VerifyingActorId,
                VerifiedCapability = v.VerifiedCapability,
                GrantedAuthorityScopeKind = v.GrantedAuthorityScopeKind.ToString(),
                GrantedAuthorityScopeIdentifier = v.GrantedAuthorityScopeIdentifier,
                RequiredAuthorityScopeKind = v.RequiredAuthorityScopeKind.ToString(),
                RequiredAuthorityScopeIdentifier = v.RequiredAuthorityScopeIdentifier,
                AuthorityValidFrom = v.AuthorityValidFrom.ToUniversalTime(),
                AuthorityVerificationTime = v.AuthorityVerificationTime.ToUniversalTime(),
                AuthorityValidUntil = v.AuthorityValidUntil.ToUniversalTime()
            };

            return new HumanFactVerificationEntity
            {
                Id = v.Id.Value,
                DocumentAnalysisId = domain.Id.Value,
                FactKey = v.FactCode.Value,
                Action = v.Decision.ToString(),
                CorrectedValue = v.CorrectedValue?.CanonicalValue,
                VerifiedByActorId = v.VerifyingActorId,
                VerifiedAt = v.VerifiedAt.ToUniversalTime(),
                Note = PersistenceJsonSerializer.Serialize(dto)
            };
        }).ToList();

        entity.VerifiedFactSnapshots = domain.VerifiedFactSnapshots.Select(s =>
        {
            return new VerifiedFactSnapshotEntity
            {
                Id = s.Id.Value,
                DocumentAnalysisId = s.DocumentAnalysisId.Value,
                GovernedDocumentId = s.GovernedDocumentId.Value,
                DocumentVersionId = s.DocumentVersionId.Value,
                ChecksumAlgorithm = s.DocumentChecksum.Algorithm,
                ChecksumValue = s.DocumentChecksum.Value,
                AnalysisRunId = s.AnalysisRunId.Value,
                AnalysisRunResultId = s.AnalysisRunResultId.Value,
                RunNumber = s.RunNumber,
                ModelProvider = s.ModelReference.Provider,
                ModelName = s.ModelReference.ModelName,
                ModelVersion = s.ModelReference.ModelVersion,
                RequestedCapabilitiesJson = PersistenceJsonSerializer.Serialize(s.RequestedCapabilities.Select(c => c.Value).ToList()),
                ResultOutcome = s.ResultOutcome.ToString(),
                SnapshotOutcome = s.SnapshotOutcome.ToString(),
                EntriesJson = PersistenceJsonSerializer.Serialize(s.Entries.Select(e => new VerifiedFactEntryDto
                {
                    SourceExtractedFactId = e.SourceExtractedFactId?.Value,
                    HumanEvidence = e.HumanEvidence,
                    VerificationId = e.VerificationId.Value,
                    FactCode = e.FactCode.Value,
                    EffectiveValueKind = e.EffectiveValue.Kind.ToString(),
                    EffectiveValueCanonical = e.EffectiveValue.CanonicalValue,
                    Decision = e.Decision.ToString(),
                    VerifyingActorId = e.VerifyingActorId,
                    VerifiedAt = e.VerifiedAt.ToUniversalTime()
                }).ToList()),
                SourceFactCount = s.SourceFactCount,
                PublishedFactCount = s.PublishedFactCount,
                ConfirmedFactCount = s.ConfirmedFactCount,
                CorrectedFactCount = s.CorrectedFactCount,
                UnsupportedFactCount = s.UnsupportedFactCount,
                PublishedAt = s.PublishedAt.ToUniversalTime(),
                PublishingActorId = s.PublishingActorId,
                VerifiedCapability = s.VerifiedCapability,
                GrantedAuthorityScopeKind = s.GrantedAuthorityScopeKind.ToString(),
                GrantedAuthorityScopeIdentifier = s.GrantedAuthorityScopeIdentifier,
                RequiredAuthorityScopeKind = s.RequiredAuthorityScopeKind.ToString(),
                RequiredAuthorityScopeIdentifier = s.RequiredAuthorityScopeIdentifier,
                AuthorityValidFrom = s.AuthorityValidFrom.ToUniversalTime(),
                AuthorityVerificationTime = s.AuthorityVerificationTime.ToUniversalTime(),
                AuthorityValidUntil = s.AuthorityValidUntil.ToUniversalTime()
            };
        }).ToList();

        return entity;
    }

    public static DocumentAnalysis ToDomain(DocumentAnalysisEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var domain = AggregateRehydrator.Create<DocumentAnalysis>();

        AggregateRehydrator.SetPropertyBackingField(domain, nameof(DocumentAnalysis.Id), new DocumentAnalysisId(entity.Id));
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(DocumentAnalysis.GovernedDocumentId), new GovernedDocumentId(entity.GovernedDocumentId));
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(DocumentAnalysis.DocumentVersionId), new DocumentVersionId(entity.DocumentVersionId));
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(DocumentAnalysis.DocumentChecksum), new DocumentChecksum(entity.ChecksumAlgorithm, entity.ChecksumValue));
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(DocumentAnalysis.DocumentVersionNumber), entity.DocumentVersionNumber);
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(DocumentAnalysis.SourceDocumentRevision), entity.SourceDocumentRevision);
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(DocumentAnalysis.CreatedAt), DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc));
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(DocumentAnalysis.Revision), entity.Revision);

        // Rehydrate runs
        var runs = new List<AnalysisRun>();
        foreach (var r in entity.Runs.OrderBy(x => x.RunNumber))
        {
            var run = AggregateRehydrator.Create<AnalysisRun>();
            AggregateRehydrator.SetPropertyBackingField(run, nameof(AnalysisRun.Id), new AnalysisRunId(r.Id));
            AggregateRehydrator.SetPropertyBackingField(run, nameof(AnalysisRun.RunNumber), r.RunNumber);
            AggregateRehydrator.SetPropertyBackingField(run, nameof(AnalysisRun.DocumentVersionId), new DocumentVersionId(r.DocumentVersionId));
            AggregateRehydrator.SetPropertyBackingField(run, nameof(AnalysisRun.DocumentChecksum), new DocumentChecksum(r.ChecksumAlgorithm, r.ChecksumValue));
            AggregateRehydrator.SetPropertyBackingField(run, nameof(AnalysisRun.ModelReference), new AnalysisModelReference(r.ModelProvider, r.ModelName, r.ModelVersion));

            var capStrings = PersistenceJsonSerializer.Deserialize<List<string>>(r.RequestedCapabilitiesJson) ?? new List<string>();
            var caps = capStrings.Select(c => new AnalysisCapabilityCode(c)).ToList();
            AggregateRehydrator.SetField(run, "_requestedCapabilities", caps);

            AggregateRehydrator.SetPropertyBackingField(run, nameof(AnalysisRun.RequestedAt), DateTime.SpecifyKind(r.RequestedAt, DateTimeKind.Utc));
            AggregateRehydrator.SetPropertyBackingField(run, nameof(AnalysisRun.State), Enum.Parse<AnalysisRunState>(r.State));

            if (r.StartedAt.HasValue)
                AggregateRehydrator.SetPropertyBackingField(run, nameof(AnalysisRun.StartedAt), (DateTime?)DateTime.SpecifyKind(r.StartedAt.Value, DateTimeKind.Utc));
            if (r.CompletedAt.HasValue)
                AggregateRehydrator.SetPropertyBackingField(run, nameof(AnalysisRun.CompletedAt), (DateTime?)DateTime.SpecifyKind(r.CompletedAt.Value, DateTimeKind.Utc));
            if (r.FailedAt.HasValue)
                AggregateRehydrator.SetPropertyBackingField(run, nameof(AnalysisRun.FailedAt), (DateTime?)DateTime.SpecifyKind(r.FailedAt.Value, DateTimeKind.Utc));
            if (r.SupersededAt.HasValue)
                AggregateRehydrator.SetPropertyBackingField(run, nameof(AnalysisRun.SupersededAt), (DateTime?)DateTime.SpecifyKind(r.SupersededAt.Value, DateTimeKind.Utc));
            
            AggregateRehydrator.SetPropertyBackingField(run, nameof(AnalysisRun.SupersessionReason), r.SupersessionReason);

            if (!string.IsNullOrWhiteSpace(r.FailureJson))
            {
                var failureDto = PersistenceJsonSerializer.Deserialize<AnalysisRunFailureDto>(r.FailureJson);
                if (failureDto != null)
                {
                    AggregateRehydrator.SetPropertyBackingField(run, nameof(AnalysisRun.Failure), new AnalysisRunFailure(failureDto.Code, failureDto.Description));
                }
            }

            if (!string.IsNullOrWhiteSpace(r.ResultJson))
            {
                var result = DeserializeAnalysisRunResult(r.ResultJson);
                AggregateRehydrator.SetPropertyBackingField(run, nameof(AnalysisRun.Result), result);
            }

            runs.Add(run);
        }
        AggregateRehydrator.SetField(domain, "_runs", runs);

        // Rehydrate human verifications
        var verifications = new List<HumanFactVerification>();
        foreach (var v in entity.Verifications)
        {
            if (!string.IsNullOrWhiteSpace(v.Note))
            {
                var dto = PersistenceJsonSerializer.Deserialize<HumanFactVerificationDto>(v.Note);
                if (dto != null)
                {
                    var verification = AggregateRehydrator.Create<HumanFactVerification>();
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.Id), new HumanFactVerificationId(dto.Id));
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.DocumentAnalysisId), new DocumentAnalysisId(dto.DocumentAnalysisId));
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.AnalysisRunId), new AnalysisRunId(dto.AnalysisRunId));
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.AnalysisRunResultId), new AnalysisRunResultId(dto.AnalysisRunResultId));
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.ExtractedFactId), new ExtractedFactId(dto.ExtractedFactId));
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.DocumentVersionId), new DocumentVersionId(dto.DocumentVersionId));
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.DocumentChecksum), new DocumentChecksum(dto.ChecksumAlgorithm, dto.ChecksumValue));
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.RunNumber), dto.RunNumber);
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.ModelReference), new AnalysisModelReference(dto.ModelProvider, dto.ModelName, dto.ModelVersion));
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.FactCode), new FactCode(dto.FactCode));
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.OriginalValue), new AnalysisFactValue(Enum.Parse<AnalysisFactValueKind>(dto.OriginalValueKind), dto.OriginalCanonicalValue));
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.Decision), Enum.Parse<FactVerificationDecision>(dto.Decision));

                    if (dto.CorrectedValueKind != null && dto.CorrectedCanonicalValue != null)
                    {
                        AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.CorrectedValue), new AnalysisFactValue(Enum.Parse<AnalysisFactValueKind>(dto.CorrectedValueKind), dto.CorrectedCanonicalValue));
                    }

                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.Reason), dto.Reason);
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.VerifiedAt), DateTime.SpecifyKind(dto.VerifiedAt, DateTimeKind.Utc));
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.VerifyingActorId), dto.VerifyingActorId);
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.VerifiedCapability), dto.VerifiedCapability);
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.GrantedAuthorityScopeKind), Enum.Parse<AuthorityScopeKind>(dto.GrantedAuthorityScopeKind));
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.GrantedAuthorityScopeIdentifier), dto.GrantedAuthorityScopeIdentifier);
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.RequiredAuthorityScopeKind), Enum.Parse<AuthorityScopeKind>(dto.RequiredAuthorityScopeKind));
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.RequiredAuthorityScopeIdentifier), dto.RequiredAuthorityScopeIdentifier);
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.AuthorityValidFrom), DateTime.SpecifyKind(dto.AuthorityValidFrom, DateTimeKind.Utc));
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.AuthorityVerificationTime), DateTime.SpecifyKind(dto.AuthorityVerificationTime, DateTimeKind.Utc));
                    AggregateRehydrator.SetPropertyBackingField(verification, nameof(HumanFactVerification.AuthorityValidUntil), DateTime.SpecifyKind(dto.AuthorityValidUntil, DateTimeKind.Utc));

                    verifications.Add(verification);
                }
            }
        }
        AggregateRehydrator.SetField(domain, "_verifications", verifications);

        // Rehydrate snapshots
        var snapshots = new List<VerifiedFactSnapshot>();
        foreach (var s in entity.VerifiedFactSnapshots.OrderBy(s => s.PublishedAt).ThenBy(s => s.Id))
        {
            snapshots.Add(ToDomainSnapshot(s));
        }
        AggregateRehydrator.SetField(domain, "_verifiedFactSnapshots", snapshots);

        AggregateRehydrator.SetField(domain, "_domainEvents", new List<IDomainEvent>());

        return domain;
    }

    public static VerifiedFactSnapshot ToDomainSnapshot(VerifiedFactSnapshotEntity s)
    {
        ArgumentNullException.ThrowIfNull(s);

        var snapshot = AggregateRehydrator.Create<VerifiedFactSnapshot>();
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.Id), new VerifiedFactSnapshotId(s.Id));
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.DocumentAnalysisId), new DocumentAnalysisId(s.DocumentAnalysisId));
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.GovernedDocumentId), new GovernedDocumentId(s.GovernedDocumentId));
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.DocumentVersionId), new DocumentVersionId(s.DocumentVersionId));
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.DocumentChecksum), new DocumentChecksum(s.ChecksumAlgorithm, s.ChecksumValue));
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.AnalysisRunId), new AnalysisRunId(s.AnalysisRunId));
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.AnalysisRunResultId), new AnalysisRunResultId(s.AnalysisRunResultId));
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.RunNumber), s.RunNumber);
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.ModelReference), new AnalysisModelReference(s.ModelProvider, s.ModelName, s.ModelVersion));

        var snapCapStrings = PersistenceJsonSerializer.Deserialize<List<string>>(s.RequestedCapabilitiesJson) ?? new List<string>();
        var snapCaps = snapCapStrings.Select(c => new AnalysisCapabilityCode(c)).ToList();
        AggregateRehydrator.SetField(snapshot, "_requestedCapabilities", snapCaps);

        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.ResultOutcome), Enum.Parse<AnalysisResultOutcome>(s.ResultOutcome));
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.SnapshotOutcome), Enum.Parse<VerifiedFactSnapshotOutcome>(s.SnapshotOutcome));

        var entryDtos = PersistenceJsonSerializer.Deserialize<List<VerifiedFactEntryDto>>(s.EntriesJson) ?? new List<VerifiedFactEntryDto>();
        var entries = entryDtos.Select(ed =>
        {
            var entry = AggregateRehydrator.Create<VerifiedFactEntry>();
            AggregateRehydrator.SetPropertyBackingField(entry, nameof(VerifiedFactEntry.SourceExtractedFactId), ed.SourceExtractedFactId.HasValue ? new ExtractedFactId(ed.SourceExtractedFactId.Value) : null);
            AggregateRehydrator.SetPropertyBackingField(entry, nameof(VerifiedFactEntry.HumanEvidence), ed.HumanEvidence);
            AggregateRehydrator.SetPropertyBackingField(entry, nameof(VerifiedFactEntry.VerificationId), new HumanFactVerificationId(ed.VerificationId));
            AggregateRehydrator.SetPropertyBackingField(entry, nameof(VerifiedFactEntry.FactCode), new FactCode(ed.FactCode));
            AggregateRehydrator.SetPropertyBackingField(entry, nameof(VerifiedFactEntry.EffectiveValue), new AnalysisFactValue(Enum.Parse<AnalysisFactValueKind>(ed.EffectiveValueKind), ed.EffectiveValueCanonical));
            AggregateRehydrator.SetPropertyBackingField(entry, nameof(VerifiedFactEntry.Decision), Enum.Parse<FactVerificationDecision>(ed.Decision));
            AggregateRehydrator.SetPropertyBackingField(entry, nameof(VerifiedFactEntry.VerifyingActorId), ed.VerifyingActorId);
            AggregateRehydrator.SetPropertyBackingField(entry, nameof(VerifiedFactEntry.VerifiedAt), DateTime.SpecifyKind(ed.VerifiedAt, DateTimeKind.Utc));
            return entry;
        }).ToList();
        AggregateRehydrator.SetField(snapshot, "_entries", entries);

        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.SourceFactCount), s.SourceFactCount);
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.PublishedFactCount), s.PublishedFactCount);
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.ConfirmedFactCount), s.ConfirmedFactCount);
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.CorrectedFactCount), s.CorrectedFactCount);
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.UnsupportedFactCount), s.UnsupportedFactCount);
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.PublishedAt), DateTime.SpecifyKind(s.PublishedAt, DateTimeKind.Utc));
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.PublishingActorId), s.PublishingActorId);
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.VerifiedCapability), s.VerifiedCapability);
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.GrantedAuthorityScopeKind), Enum.Parse<AuthorityScopeKind>(s.GrantedAuthorityScopeKind));
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.GrantedAuthorityScopeIdentifier), s.GrantedAuthorityScopeIdentifier);
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.RequiredAuthorityScopeKind), Enum.Parse<AuthorityScopeKind>(s.RequiredAuthorityScopeKind));
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.RequiredAuthorityScopeIdentifier), s.RequiredAuthorityScopeIdentifier);
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.AuthorityValidFrom), DateTime.SpecifyKind(s.AuthorityValidFrom, DateTimeKind.Utc));
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.AuthorityVerificationTime), DateTime.SpecifyKind(s.AuthorityVerificationTime, DateTimeKind.Utc));
        AggregateRehydrator.SetPropertyBackingField(snapshot, nameof(VerifiedFactSnapshot.AuthorityValidUntil), DateTime.SpecifyKind(s.AuthorityValidUntil, DateTimeKind.Utc));

        return snapshot;
    }

    private static string SerializeAnalysisRunResult(AnalysisRunResult result)
    {
        var dto = new AnalysisRunResultDto
        {
            Id = result.Id.Value,
            AnalysisRunId = result.AnalysisRunId.Value,
            DocumentVersionId = result.DocumentVersionId.Value,
            ChecksumAlgorithm = result.DocumentChecksum.Algorithm,
            ChecksumValue = result.DocumentChecksum.Value,
            ModelProvider = result.ModelReference.Provider,
            ModelName = result.ModelReference.ModelName,
            ModelVersion = result.ModelReference.ModelVersion,
            Outcome = result.Outcome.ToString(),
            CompletedAt = result.CompletedAt.ToUniversalTime(),
            RequestedCapabilities = result.RequestedCapabilities.Select(c => c.Value).ToList(),
            Artifacts = result.Artifacts.Select(a => new AnalysisResultArtifactDto
            {
                Id = a.Id.Value,
                ArtifactKind = a.ArtifactKind,
                StorageReference = a.StorageReference,
                ContentType = a.ContentType,
                ChecksumAlgorithm = a.Checksum.Algorithm,
                ChecksumValue = a.Checksum.Value
            }).ToList(),
            ExtractedFacts = result.ExtractedFacts.Select(f => new ExtractedFactDto
            {
                Id = f.Id.Value,
                AnalysisRunResultId = f.AnalysisRunResultId.Value,
                FactCode = f.FactCode.Value,
                FactValueKind = f.FactValue.Kind.ToString(),
                FactCanonicalValue = f.FactValue.CanonicalValue,
                ConfidenceScore = f.ConfidenceScore?.Value,
                EvidenceArtifactId = f.EvidenceReference?.ArtifactId.Value,
                EvidencePageNumber = f.EvidenceReference?.PageNumber,
                EvidenceExcerpt = f.EvidenceReference?.Excerpt
            }).ToList()
        };

        return PersistenceJsonSerializer.Serialize(dto);
    }

    private static AnalysisRunResult DeserializeAnalysisRunResult(string json)
    {
        var dto = PersistenceJsonSerializer.Deserialize<AnalysisRunResultDto>(json)!;

        var result = AggregateRehydrator.Create<AnalysisRunResult>();
        AggregateRehydrator.SetPropertyBackingField(result, nameof(AnalysisRunResult.Id), new AnalysisRunResultId(dto.Id));
        AggregateRehydrator.SetPropertyBackingField(result, nameof(AnalysisRunResult.AnalysisRunId), new AnalysisRunId(dto.AnalysisRunId));
        AggregateRehydrator.SetPropertyBackingField(result, nameof(AnalysisRunResult.DocumentVersionId), new DocumentVersionId(dto.DocumentVersionId));
        AggregateRehydrator.SetPropertyBackingField(result, nameof(AnalysisRunResult.DocumentChecksum), new DocumentChecksum(dto.ChecksumAlgorithm, dto.ChecksumValue));
        AggregateRehydrator.SetPropertyBackingField(result, nameof(AnalysisRunResult.ModelReference), new AnalysisModelReference(dto.ModelProvider, dto.ModelName, dto.ModelVersion));
        AggregateRehydrator.SetPropertyBackingField(result, nameof(AnalysisRunResult.Outcome), Enum.Parse<AnalysisResultOutcome>(dto.Outcome));
        AggregateRehydrator.SetPropertyBackingField(result, nameof(AnalysisRunResult.CompletedAt), DateTime.SpecifyKind(dto.CompletedAt, DateTimeKind.Utc));

        var caps = (dto.RequestedCapabilities ?? new List<string>()).Select(c => new AnalysisCapabilityCode(c)).ToList();
        AggregateRehydrator.SetField(result, "_requestedCapabilities", caps);

        var artifacts = (dto.Artifacts ?? new List<AnalysisResultArtifactDto>()).Select(a =>
            new AnalysisResultArtifactReference(
                new AnalysisResultArtifactId(a.Id),
                a.ArtifactKind,
                a.StorageReference,
                a.ContentType,
                new AnalysisArtifactChecksum(a.ChecksumAlgorithm, a.ChecksumValue))
        ).ToList();
        AggregateRehydrator.SetField(result, "_artifacts", artifacts);

        var facts = (dto.ExtractedFacts ?? new List<ExtractedFactDto>()).Select(f =>
        {
            var fact = AggregateRehydrator.Create<ExtractedFact>();
            AggregateRehydrator.SetPropertyBackingField(fact, nameof(ExtractedFact.Id), new ExtractedFactId(f.Id));
            AggregateRehydrator.SetPropertyBackingField(fact, nameof(ExtractedFact.AnalysisRunResultId), new AnalysisRunResultId(f.AnalysisRunResultId));
            AggregateRehydrator.SetPropertyBackingField(fact, nameof(ExtractedFact.FactCode), new FactCode(f.FactCode));
            AggregateRehydrator.SetPropertyBackingField(fact, nameof(ExtractedFact.FactValue), new AnalysisFactValue(Enum.Parse<AnalysisFactValueKind>(f.FactValueKind), f.FactCanonicalValue));

            if (f.ConfidenceScore.HasValue)
                AggregateRehydrator.SetPropertyBackingField(fact, nameof(ExtractedFact.ConfidenceScore), new ConfidenceScore(f.ConfidenceScore.Value));

            if (f.EvidenceArtifactId.HasValue)
            {
                var evidence = new AnalysisEvidenceReference(new AnalysisResultArtifactId(f.EvidenceArtifactId.Value), f.EvidencePageNumber, f.EvidenceExcerpt);
                AggregateRehydrator.SetPropertyBackingField(fact, nameof(ExtractedFact.EvidenceReference), evidence);
            }

            return fact;
        }).ToList();
        AggregateRehydrator.SetField(result, "_extractedFacts", facts);

        return result;
    }

    private sealed record AnalysisRunFailureDto(string Code, string Description);

    private sealed class AnalysisRunResultDto
    {
        public Guid Id { get; set; }
        public Guid AnalysisRunId { get; set; }
        public Guid DocumentVersionId { get; set; }
        public string ChecksumAlgorithm { get; set; } = string.Empty;
        public string ChecksumValue { get; set; } = string.Empty;
        public string ModelProvider { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string ModelVersion { get; set; } = string.Empty;
        public string Outcome { get; set; } = string.Empty;
        public DateTime CompletedAt { get; set; }
        public List<string> RequestedCapabilities { get; set; } = new();
        public List<AnalysisResultArtifactDto> Artifacts { get; set; } = new();
        public List<ExtractedFactDto> ExtractedFacts { get; set; } = new();
    }

    private sealed class AnalysisResultArtifactDto
    {
        public Guid Id { get; set; }
        public string ArtifactKind { get; set; } = string.Empty;
        public string StorageReference { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string ChecksumAlgorithm { get; set; } = string.Empty;
        public string ChecksumValue { get; set; } = string.Empty;
    }

    private sealed class ExtractedFactDto
    {
        public Guid Id { get; set; }
        public Guid AnalysisRunResultId { get; set; }
        public string FactCode { get; set; } = string.Empty;
        public string FactValueKind { get; set; } = string.Empty;
        public string FactCanonicalValue { get; set; } = string.Empty;
        public decimal? ConfidenceScore { get; set; }
        public Guid? EvidenceArtifactId { get; set; }
        public int? EvidencePageNumber { get; set; }
        public string? EvidenceExcerpt { get; set; }
    }

    private sealed class HumanFactVerificationDto
    {
        public Guid Id { get; set; }
        public Guid DocumentAnalysisId { get; set; }
        public Guid AnalysisRunId { get; set; }
        public Guid AnalysisRunResultId { get; set; }
        public Guid ExtractedFactId { get; set; }
        public Guid DocumentVersionId { get; set; }
        public string ChecksumAlgorithm { get; set; } = string.Empty;
        public string ChecksumValue { get; set; } = string.Empty;
        public int RunNumber { get; set; }
        public string ModelProvider { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string ModelVersion { get; set; } = string.Empty;
        public string FactCode { get; set; } = string.Empty;
        public string OriginalValueKind { get; set; } = string.Empty;
        public string OriginalCanonicalValue { get; set; } = string.Empty;
        public string Decision { get; set; } = string.Empty;
        public string? CorrectedValueKind { get; set; }
        public string? CorrectedCanonicalValue { get; set; }
        public string? Reason { get; set; }
        public DateTime VerifiedAt { get; set; }
        public Guid VerifyingActorId { get; set; }
        public string VerifiedCapability { get; set; } = string.Empty;
        public string GrantedAuthorityScopeKind { get; set; } = string.Empty;
        public string? GrantedAuthorityScopeIdentifier { get; set; }
        public string RequiredAuthorityScopeKind { get; set; } = string.Empty;
        public string RequiredAuthorityScopeIdentifier { get; set; } = string.Empty;
        public DateTime AuthorityValidFrom { get; set; }
        public DateTime AuthorityVerificationTime { get; set; }
        public DateTime AuthorityValidUntil { get; set; }
    }

    private sealed class VerifiedFactEntryDto
    {
        public Guid? SourceExtractedFactId { get; set; }
        public HumanEvidenceFact? HumanEvidence { get; set; }
        public Guid VerificationId { get; set; }
        public string FactCode { get; set; } = string.Empty;
        public string EffectiveValueKind { get; set; } = string.Empty;
        public string EffectiveValueCanonical { get; set; } = string.Empty;
        public string Decision { get; set; } = string.Empty;
        public Guid VerifyingActorId { get; set; }
        public DateTime VerifiedAt { get; set; }
    }
}
