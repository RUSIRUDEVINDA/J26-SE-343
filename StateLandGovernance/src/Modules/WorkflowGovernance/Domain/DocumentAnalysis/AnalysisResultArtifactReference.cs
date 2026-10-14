namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class AnalysisResultArtifactReference : IEquatable<AnalysisResultArtifactReference>
{
    public AnalysisResultArtifactId Id { get; }
    public string ArtifactKind { get; }
    public string StorageReference { get; }
    public string ContentType { get; }
    public AnalysisArtifactChecksum Checksum { get; }

    public AnalysisResultArtifactReference(AnalysisResultArtifactId id, string artifactKind, string storageReference, string contentType, AnalysisArtifactChecksum checksum)
    {
        if (id.Value == Guid.Empty) throw new InvalidAnalysisResultArtifactException("Id required.");
        if (string.IsNullOrWhiteSpace(artifactKind)) throw new InvalidAnalysisResultArtifactException("ArtifactKind required.");
        var kindTrim = artifactKind.Trim();
        if (kindTrim.Length > 100) throw new InvalidAnalysisResultArtifactException("ArtifactKind too long.");
        foreach (char c in kindTrim) { if (!char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-') throw new InvalidAnalysisResultArtifactException("Invalid ArtifactKind."); }

        if (string.IsNullOrWhiteSpace(storageReference)) throw new InvalidAnalysisResultArtifactException("StorageReference required.");
        var storeTrim = storageReference.Trim();
        if (storeTrim.Length > 500) throw new InvalidAnalysisResultArtifactException("StorageReference too long.");
        if (storeTrim.Contains("://") || storeTrim.Contains("?") || storeTrim.Contains("#") || storeTrim.StartsWith("/") || storeTrim.StartsWith("\\") || storeTrim.Contains("\\") || storeTrim.Contains(":"))
            throw new InvalidAnalysisResultArtifactException("Invalid StorageReference.");
        var parts = storeTrim.Split('/');
        foreach(var p in parts) { if (p == "" || p == "." || p == "..") throw new InvalidAnalysisResultArtifactException("Invalid StorageReference segment."); }
        foreach (char c in storeTrim) { if (char.IsControl(c)) throw new InvalidAnalysisResultArtifactException("Control chars in StorageReference."); }

        if (string.IsNullOrWhiteSpace(contentType)) throw new InvalidAnalysisResultArtifactException("ContentType required.");
        var typeTrim = contentType.Trim();
        if (typeTrim.Length > 100) throw new InvalidAnalysisResultArtifactException("ContentType too long.");
        
        var splitTypes = typeTrim.Split('/');
        if (splitTypes.Length != 2 || string.IsNullOrWhiteSpace(splitTypes[0]) || string.IsNullOrWhiteSpace(splitTypes[1])) 
            throw new InvalidAnalysisResultArtifactException("Invalid ContentType shape.");

        bool IsValidToken(string s) => s.All(c => char.IsAsciiLetterOrDigit(c) || c == '!' || c == '#' || c == '$' || c == '%' || c == '&' || c == '\'' || c == '*' || c == '+' || c == '-' || c == '.' || c == '^' || c == '_' || c == '`' || c == '|' || c == '~');
        
        if (!IsValidToken(splitTypes[0]) || !IsValidToken(splitTypes[1]))
            throw new InvalidAnalysisResultArtifactException("Invalid token chars in ContentType.");

        Id = id;
        ArtifactKind = kindTrim;
        StorageReference = storeTrim;
        ContentType = typeTrim;
        Checksum = checksum ?? throw new InvalidAnalysisResultArtifactException("Checksum required.");
    }
    public bool Equals(AnalysisResultArtifactReference? other)
    {
        if (other is null) return false;
        return Id.Equals(other.Id);
    }
    public override bool Equals(object? obj) => Equals(obj as AnalysisResultArtifactReference);
    public override int GetHashCode() => Id.GetHashCode();
}

