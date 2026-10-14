namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

/// <summary>
/// Scoped session tracker managing aggregate root identity map, pending additions,
/// and baseline revisions for optimistic concurrency detection.
/// </summary>
internal sealed class WorkflowGovernanceAggregateTracker
{
    private readonly Dictionary<Guid, TrackedEntry<LeaseCase>> _leaseCases = new();
    private readonly Dictionary<Guid, TrackedEntry<GovernedDocument>> _governedDocuments = new();
    private readonly Dictionary<Guid, TrackedEntry<DocumentAnalysis>> _documentAnalyses = new();
    private readonly Dictionary<Guid, TrackedEntry<DocumentCompletenessAssessment>> _completenessAssessments = new();

    public void TrackLoaded(LeaseCase domain, int originalRevision)
    {
        _leaseCases[domain.Id.Value] = new TrackedEntry<LeaseCase>(domain, isNew: false, originalRevision);
    }

    public void TrackNew(LeaseCase domain)
    {
        _leaseCases[domain.Id.Value] = new TrackedEntry<LeaseCase>(domain, isNew: true, domain.Revision);
    }

    public bool TryGetLeaseCase(LeaseCaseId id, out LeaseCase? domain)
    {
        if (_leaseCases.TryGetValue(id.Value, out var entry))
        {
            domain = entry.Aggregate;
            return true;
        }

        domain = null;
        return false;
    }

    public void TrackLoaded(GovernedDocument domain, int originalRevision)
    {
        _governedDocuments[domain.Id.Value] = new TrackedEntry<GovernedDocument>(domain, isNew: false, originalRevision);
    }

    public void TrackNew(GovernedDocument domain)
    {
        _governedDocuments[domain.Id.Value] = new TrackedEntry<GovernedDocument>(domain, isNew: true, domain.Revision);
    }

    public bool TryGetGovernedDocument(GovernedDocumentId id, out GovernedDocument? domain)
    {
        if (_governedDocuments.TryGetValue(id.Value, out var entry))
        {
            domain = entry.Aggregate;
            return true;
        }

        domain = null;
        return false;
    }

    public void TrackLoaded(DocumentAnalysis domain, int originalRevision)
    {
        _documentAnalyses[domain.Id.Value] = new TrackedEntry<DocumentAnalysis>(domain, isNew: false, originalRevision);
    }

    public void TrackNew(DocumentAnalysis domain)
    {
        _documentAnalyses[domain.Id.Value] = new TrackedEntry<DocumentAnalysis>(domain, isNew: true, domain.Revision);
    }

    public bool TryGetDocumentAnalysis(DocumentAnalysisId id, out DocumentAnalysis? domain)
    {
        if (_documentAnalyses.TryGetValue(id.Value, out var entry))
        {
            domain = entry.Aggregate;
            return true;
        }

        domain = null;
        return false;
    }

    public bool TryGetDocumentAnalysisByVersion(DocumentVersionId versionId, out DocumentAnalysis? domain)
    {
        foreach (var entry in _documentAnalyses.Values)
        {
            if (entry.Aggregate.DocumentVersionId == versionId)
            {
                domain = entry.Aggregate;
                return true;
            }
        }

        domain = null;
        return false;
    }

    public void TrackLoaded(DocumentCompletenessAssessment domain, int originalRevision)
    {
        _completenessAssessments[domain.Id.Value] = new TrackedEntry<DocumentCompletenessAssessment>(domain, isNew: false, originalRevision);
    }

    public void TrackNew(DocumentCompletenessAssessment domain)
    {
        _completenessAssessments[domain.Id.Value] = new TrackedEntry<DocumentCompletenessAssessment>(domain, isNew: true, domain.Revision);
    }

    public bool TryGetDocumentCompletenessAssessment(DocumentCompletenessAssessmentId id, out DocumentCompletenessAssessment? domain)
    {
        if (_completenessAssessments.TryGetValue(id.Value, out var entry))
        {
            domain = entry.Aggregate;
            return true;
        }

        domain = null;
        return false;
    }

    public IEnumerable<TrackedEntry<LeaseCase>> GetTrackedLeaseCases() => _leaseCases.Values;
    public IEnumerable<TrackedEntry<GovernedDocument>> GetTrackedGovernedDocuments() => _governedDocuments.Values;
    public IEnumerable<TrackedEntry<DocumentAnalysis>> GetTrackedDocumentAnalyses() => _documentAnalyses.Values;
    public IEnumerable<TrackedEntry<DocumentCompletenessAssessment>> GetTrackedDocumentCompletenessAssessments() => _completenessAssessments.Values;

    public void AcceptAllChanges()
    {
        foreach (var key in _leaseCases.Keys)
        {
            var e = _leaseCases[key];
            _leaseCases[key] = new TrackedEntry<LeaseCase>(e.Aggregate, isNew: false, e.Aggregate.Revision);
        }

        foreach (var key in _governedDocuments.Keys)
        {
            var e = _governedDocuments[key];
            _governedDocuments[key] = new TrackedEntry<GovernedDocument>(e.Aggregate, isNew: false, e.Aggregate.Revision);
        }

        foreach (var key in _documentAnalyses.Keys)
        {
            var e = _documentAnalyses[key];
            _documentAnalyses[key] = new TrackedEntry<DocumentAnalysis>(e.Aggregate, isNew: false, e.Aggregate.Revision);
        }

        foreach (var key in _completenessAssessments.Keys)
        {
            var e = _completenessAssessments[key];
            _completenessAssessments[key] = new TrackedEntry<DocumentCompletenessAssessment>(e.Aggregate, isNew: false, e.Aggregate.Revision);
        }
    }
}

internal sealed class TrackedEntry<T>
{
    public T Aggregate { get; }
    public bool IsNew { get; }
    public int OriginalRevision { get; }

    public TrackedEntry(T aggregate, bool isNew, int originalRevision)
    {
        Aggregate = aggregate;
        IsNew = isNew;
        OriginalRevision = originalRevision;
    }
}

