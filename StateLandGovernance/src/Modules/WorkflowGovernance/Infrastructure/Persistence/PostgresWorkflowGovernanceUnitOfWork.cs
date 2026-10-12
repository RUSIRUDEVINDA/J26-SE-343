namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

public sealed class PostgresWorkflowGovernanceUnitOfWork : IWorkflowGovernanceUnitOfWork
{
    private readonly WorkflowGovernanceDbContext _dbContext;
    private readonly WorkflowGovernanceAggregateTracker _tracker;

    internal PostgresWorkflowGovernanceUnitOfWork(
        WorkflowGovernanceDbContext dbContext,
        WorkflowGovernanceAggregateTracker tracker)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        // 1. Synchronize LeaseCase aggregates
        foreach (var entry in _tracker.GetTrackedLeaseCases())
        {
            var domain = entry.Aggregate;
            if (entry.IsNew)
            {
                var entity = LeaseCasePersistenceMapper.ToPersistence(domain);
                await _dbContext.LeaseCases.AddAsync(entity, cancellationToken);
            }
            else
            {
                var entity = await _dbContext.LeaseCases
                    .FirstOrDefaultAsync(x => x.Id == domain.Id.Value, cancellationToken);

                if (entity is not null)
                {
                    _dbContext.Entry(entity).Property(e => e.Revision).OriginalValue = entry.OriginalRevision;
                    var mapped = LeaseCasePersistenceMapper.ToPersistence(domain);

                    entity.ApplicationReference = mapped.ApplicationReference;
                    entity.Status = mapped.Status;
                    entity.CurrentVerifiedFactSnapshotId = mapped.CurrentVerifiedFactSnapshotId;
                    entity.ProposalIntakeJson = mapped.ProposalIntakeJson;
                    entity.LatestScreeningJson = mapped.LatestScreeningJson;
                    entity.ActiveWorkflowPlanId = mapped.ActiveWorkflowPlanId;
                    entity.CompletedWorkflowJson = mapped.CompletedWorkflowJson;
                    entity.HandoffPackageJson = mapped.HandoffPackageJson;
                    entity.ApprovalConditionsJson = mapped.ApprovalConditionsJson;
                    entity.DocumentSubmissionRequirementsJson = mapped.DocumentSubmissionRequirementsJson;
                    entity.EscalationsJson = mapped.EscalationsJson;
                    entity.AssessmentHistoryJson = mapped.AssessmentHistoryJson;
                    entity.ProposalContentAssessmentsJson = mapped.ProposalContentAssessmentsJson;
                    entity.WorkflowPlanHistoryJson = mapped.WorkflowPlanHistoryJson;
                    entity.ProposalContentSequenceCounter = mapped.ProposalContentSequenceCounter;
                    entity.Revision = mapped.Revision;
                    entity.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        // 2. Synchronize GovernedDocument aggregates
        foreach (var entry in _tracker.GetTrackedGovernedDocuments())
        {
            var domain = entry.Aggregate;
            if (entry.IsNew)
            {
                var entity = GovernedDocumentPersistenceMapper.ToPersistence(domain);
                await _dbContext.GovernedDocuments.AddAsync(entity, cancellationToken);
            }
            else
            {
                var entity = await _dbContext.GovernedDocuments
                    .Include(x => x.Versions)
                    .FirstOrDefaultAsync(x => x.Id == domain.Id.Value, cancellationToken);

                if (entity is not null)
                {
                    _dbContext.Entry(entity).Property(e => e.Revision).OriginalValue = entry.OriginalRevision;
                    entity.ActiveVersionId = domain.ActiveVersionId.Value;
                    entity.Revision = domain.Revision;
                    entity.UpdatedAt = DateTime.UtcNow;

                    var existingVersionIds = entity.Versions.Select(v => v.Id).ToHashSet();
                    foreach (var v in domain.Versions.Where(v => !existingVersionIds.Contains(v.Id.Value)))
                    {
                        entity.Versions.Add(new DocumentVersionEntity
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
                        });
                    }
                }
            }
        }

        // 3. Synchronize DocumentAnalysis aggregates
        foreach (var entry in _tracker.GetTrackedDocumentAnalyses())
        {
            var domain = entry.Aggregate;
            if (entry.IsNew)
            {
                var entity = DocumentAnalysisPersistenceMapper.ToPersistence(domain);
                await _dbContext.DocumentAnalyses.AddAsync(entity, cancellationToken);
            }
            else
            {
                var entity = await _dbContext.DocumentAnalyses
                    .Include(x => x.Runs)
                    .Include(x => x.Verifications)
                    .Include(x => x.VerifiedFactSnapshots)
                    .FirstOrDefaultAsync(x => x.Id == domain.Id.Value, cancellationToken);

                if (entity is not null)
                {
                    _dbContext.Entry(entity).Property(e => e.Revision).OriginalValue = entry.OriginalRevision;
                    entity.Revision = domain.Revision;
                    entity.UpdatedAt = DateTime.UtcNow;

                    var mapped = DocumentAnalysisPersistenceMapper.ToPersistence(domain);

                    // Sync runs
                    var existingRuns = entity.Runs.ToDictionary(r => r.Id);
                    foreach (var run in mapped.Runs)
                    {
                        if (existingRuns.TryGetValue(run.Id, out var existingRun))
                        {
                            existingRun.State = run.State;
                            existingRun.StartedAt = run.StartedAt;
                            existingRun.CompletedAt = run.CompletedAt;
                            existingRun.FailedAt = run.FailedAt;
                            existingRun.SupersededAt = run.SupersededAt;
                            existingRun.SupersessionReason = run.SupersessionReason;
                            existingRun.FailureJson = run.FailureJson;
                            existingRun.ResultJson = run.ResultJson;
                        }
                        else
                        {
                            entity.Runs.Add(run);
                        }
                    }

                    // Sync verifications
                    var existingVerifIds = entity.Verifications.Select(v => v.Id).ToHashSet();
                    foreach (var verif in mapped.Verifications.Where(v => !existingVerifIds.Contains(v.Id)))
                    {
                        entity.Verifications.Add(verif);
                    }

                    // Sync snapshots
                    var existingSnapIds = entity.VerifiedFactSnapshots.Select(s => s.Id).ToHashSet();
                    foreach (var snap in mapped.VerifiedFactSnapshots.Where(s => !existingSnapIds.Contains(s.Id)))
                    {
                        entity.VerifiedFactSnapshots.Add(snap);
                    }
                }
            }
        }

        // 4. Synchronize DocumentCompletenessAssessment aggregates
        foreach (var entry in _tracker.GetTrackedDocumentCompletenessAssessments())
        {
            var domain = entry.Aggregate;
            if (entry.IsNew)
            {
                var entity = DocumentCompletenessAssessmentPersistenceMapper.ToPersistence(domain);
                await _dbContext.DocumentCompletenessAssessments.AddAsync(entity, cancellationToken);
            }
            else
            {
                var entity = await _dbContext.DocumentCompletenessAssessments
                    .FirstOrDefaultAsync(x => x.Id == domain.Id.Value, cancellationToken);

                if (entity is not null)
                {
                    var mapped = DocumentCompletenessAssessmentPersistenceMapper.ToPersistence(domain);
                    _dbContext.Entry(entity).Property(e => e.Revision).OriginalValue = entry.OriginalRevision;
                    entity.Outcome = mapped.Outcome;
                    entity.Revision = mapped.Revision;
                    entity.AssessedDocumentsJson = mapped.AssessedDocumentsJson;
                    entity.ClassifiedDocumentsJson = mapped.ClassifiedDocumentsJson;
                    entity.RequirementsJson = mapped.RequirementsJson;
                    entity.ReviewsJson = mapped.ReviewsJson;
                    entity.MissingRequirementsJson = mapped.MissingRequirementsJson;
                    entity.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        // 5. Execute transactionally consistent SaveChanges with exception translation
        try
        {
            var result = await _dbContext.SaveChangesAsync(cancellationToken);
            _tracker.AcceptAllChanges();
            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            TranslateConcurrencyException(ex);
            throw;
        }
        catch (DbUpdateException ex)
        {
            TranslateDbUpdateException(ex);
            throw;
        }
    }

    private void TranslateConcurrencyException(DbUpdateConcurrencyException ex)
    {
        foreach (var entry in ex.Entries)
        {
            if (entry.Entity is LeaseCaseEntity leaseCase)
            {
                var expected = (int)(entry.OriginalValues[nameof(LeaseCaseEntity.Revision)] ?? 0);
                throw new LeaseCaseConcurrencyException(leaseCase.Id, expected, leaseCase.Revision);
            }

            if (entry.Entity is GovernedDocumentEntity doc)
            {
                var expected = (int)(entry.OriginalValues[nameof(GovernedDocumentEntity.Revision)] ?? 0);
                throw new DocumentConcurrencyException(doc.Id, expected, doc.Revision);
            }

            if (entry.Entity is DocumentAnalysisEntity analysis)
            {
                var expected = (int)(entry.OriginalValues[nameof(DocumentAnalysisEntity.Revision)] ?? 0);
                throw new AnalysisConcurrencyException(analysis.Id, expected, analysis.Revision);
            }

            if (entry.Entity is DocumentCompletenessAssessmentEntity assessment)
            {
                var expected = (int)(entry.OriginalValues[nameof(DocumentCompletenessAssessmentEntity.Revision)] ?? 0);
                throw new DocumentCompletenessConcurrencyException(assessment.Id, expected, assessment.Revision);
            }
        }
    }

    private void TranslateDbUpdateException(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException pex && pex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            var constraint = pex.ConstraintName ?? string.Empty;

            if (constraint.Contains("ix_lease_cases_application_reference", StringComparison.OrdinalIgnoreCase))
            {
                var failedCase = _tracker.GetTrackedLeaseCases().FirstOrDefault(c => c.IsNew);
                var appRef = failedCase?.Aggregate.ApplicationReference ?? "unknown";
                throw new DuplicateApplicationReferenceException(appRef);
            }

            if (constraint.Contains("ix_document_analyses_document_version_id", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictingAnalysisRunException("A document analysis already exists for this document version.");
            }

            if (constraint.Contains("ix_document_versions_governed_doc_ver", StringComparison.OrdinalIgnoreCase))
            {
                throw new DuplicateDocumentVersionException("A document version with this version number already exists.");
            }
        }
    }
}

