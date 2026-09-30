namespace StateLandGovernance.WorkflowGovernance.Application.Mappings;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

public static class DocumentAnalysisMapper
{
    public static DocumentAnalysisDto ToDto(DocumentAnalysis analysis)
    {
        if (analysis == null)
        {
            throw new ArgumentNullException(nameof(analysis));
        }

        var runs = analysis.Runs
            .OrderBy(r => r.RunNumber)
            .Select(ToRunDto)
            .ToList();

        var activeRun = runs.LastOrDefault();

        return new DocumentAnalysisDto(
            Id: analysis.Id.Value,
            GovernedDocumentId: analysis.GovernedDocumentId.Value,
            DocumentVersionId: analysis.DocumentVersionId.Value,
            ChecksumAlgorithm: analysis.DocumentChecksum.Algorithm,
            ChecksumValue: analysis.DocumentChecksum.Value,
            DocumentVersionNumber: analysis.DocumentVersionNumber,
            SourceDocumentRevision: analysis.SourceDocumentRevision,
            CreatedAt: analysis.CreatedAt,
            Revision: analysis.Revision,
            RunCount: analysis.Runs.Count,
            ActiveRun: activeRun,
            Runs: runs
        );
    }

    public static AnalysisRunDto ToRunDto(AnalysisRun run)
    {
        if (run == null)
        {
            throw new ArgumentNullException(nameof(run));
        }

        return new AnalysisRunDto(
            Id: run.Id.Value,
            RunNumber: run.RunNumber,
            State: run.State.ToString(),
            ModelProvider: run.ModelReference.Provider,
            ModelName: run.ModelReference.ModelName,
            ModelVersion: run.ModelReference.ModelVersion,
            RequestedCapabilities: run.RequestedCapabilities.Select(c => c.Value).ToList(),
            RequestedAt: run.RequestedAt,
            StartedAt: run.StartedAt,
            CompletedAt: run.CompletedAt,
            FailedAt: run.FailedAt,
            SupersededAt: run.SupersededAt,
            FailureCode: run.Failure?.Code,
            FailureDescription: run.Failure?.Description,
            SupersessionReason: run.SupersessionReason,
            Result: run.Result != null ? ToResultDto(run.Result) : null
        );
    }

    public static AnalysisRunResultDto ToResultDto(AnalysisRunResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        var artifacts = result.Artifacts
            .Select(a => new AnalysisArtifactDto(
                Id: a.Id.Value,
                ArtifactKind: a.ArtifactKind,
                ContentType: a.ContentType,
                ChecksumAlgorithm: a.Checksum.Algorithm,
                ChecksumValue: a.Checksum.Value))
            .ToList();

        var facts = result.ExtractedFacts
            .Select(f => new CandidateFactDto(
                Id: f.Id.Value,
                FactCode: f.FactCode.Value,
                ValueKind: f.FactValue.Kind.ToString(),
                CandidateValue: f.FactValue.CanonicalValue,
                ConfidenceScore: f.ConfidenceScore?.Value,
                EvidenceArtifactId: f.EvidenceReference?.ArtifactId.Value,
                PageNumber: f.EvidenceReference?.PageNumber,
                Excerpt: f.EvidenceReference?.Excerpt))
            .ToList();

        return new AnalysisRunResultDto(
            Id: result.Id.Value,
            Outcome: result.Outcome.ToString(),
            CompletedAt: result.CompletedAt,
            Artifacts: artifacts,
            ExtractedCandidateFacts: facts
        );
    }
}
