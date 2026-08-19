namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class AnalysisEvidenceReference : IEquatable<AnalysisEvidenceReference>
{
    public AnalysisResultArtifactId ArtifactId { get; }
    public int? PageNumber { get; }
    public string? Excerpt { get; }

    public AnalysisEvidenceReference(AnalysisResultArtifactId artifactId, int? pageNumber, string? excerpt)
    {
        if (artifactId.Value == Guid.Empty) throw new InvalidAnalysisEvidenceException("ArtifactId required.");
        if (pageNumber.HasValue && pageNumber.Value <= 0) throw new InvalidAnalysisEvidenceException("PageNumber must be > 0.");
        
        string? trimmed = null;
        if (!string.IsNullOrWhiteSpace(excerpt))
        {
            trimmed = excerpt.Trim();
            if (trimmed.Length > 500) throw new InvalidAnalysisEvidenceException("Excerpt too long.");
            trimmed = trimmed.Replace("\r\n", "\n").Replace("\r", "\n");
            foreach(char c in trimmed) { if (char.IsControl(c) && c != '\n') throw new InvalidAnalysisEvidenceException("Control chars in Excerpt."); }
        }

        ArtifactId = artifactId;
        PageNumber = pageNumber;
        Excerpt = trimmed;
    }
    public bool Equals(AnalysisEvidenceReference? other)
    {
        if (other is null) return false;
        return ArtifactId.Equals(other.ArtifactId) && PageNumber == other.PageNumber && string.Equals(Excerpt, other.Excerpt, StringComparison.Ordinal);
    }
    public override bool Equals(object? obj) => Equals(obj as AnalysisEvidenceReference);
    public override int GetHashCode() => HashCode.Combine(ArtifactId, PageNumber, Excerpt);
}
