namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

/// <summary>
/// Repository port for the DocumentCompletenessAssessment aggregate root.
/// Manages persistence and retrieval of supporting-document completeness assessments and classification reviews.
/// </summary>
public interface IDocumentCompletenessAssessmentRepository
{
    Task<DocumentCompletenessAssessment?> GetByIdAsync(DocumentCompletenessAssessmentId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentCompletenessAssessment>> GetByLeaseCaseIdAsync(LeaseCaseId leaseCaseId, CancellationToken cancellationToken = default);

    Task AddAsync(DocumentCompletenessAssessment assessment, CancellationToken cancellationToken = default);
}
