namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

/// <summary>
/// Repository port for the DocumentAnalysis aggregate root.
/// Manages persistence and retrieval of document analysis aggregates, their runs, and machine findings.
/// 
/// Aggregate Uniqueness Invariant:
/// Exactly one DocumentAnalysis aggregate must exist per exact DocumentVersionId.
/// Re-analysis of the same version appends additional AnalysisRun children to the existing DocumentAnalysis aggregate.
/// Because Application pre-checks alone are not race-free under concurrent initial requests,
/// future Infrastructure persistence (EF Core / PostgreSQL) must enforce an authoritative
/// unique constraint/index on the version-binding key:
///   CREATE UNIQUE INDEX IX_DocumentAnalyses_DocumentVersionId ON DocumentAnalyses (DocumentVersionId);
/// and translate database duplicate key violations into appropriate conflict/concurrency exceptions.
/// </summary>
public interface IDocumentAnalysisRepository
{
    Task<DocumentAnalysis?> GetByIdAsync(DocumentAnalysisId id, CancellationToken cancellationToken = default);
    Task<DocumentAnalysis?> GetByDocumentVersionIdAsync(DocumentVersionId documentVersionId, CancellationToken cancellationToken = default);
    Task AddAsync(DocumentAnalysis documentAnalysis, CancellationToken cancellationToken = default);
}
