namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class AnalysisModelReference : IEquatable<AnalysisModelReference>
{
    public string Provider { get; }
    public string ModelName { get; }
    public string ModelVersion { get; }
    
    public AnalysisModelReference(string provider, string modelName, string modelVersion)
    {
        if (string.IsNullOrWhiteSpace(provider)) throw new InvalidAnalysisRunException("Provider cannot be blank.");
        if (string.IsNullOrWhiteSpace(modelName)) throw new InvalidAnalysisRunException("ModelName cannot be blank.");
        if (string.IsNullOrWhiteSpace(modelVersion)) throw new InvalidAnalysisRunException("ModelVersion cannot be blank.");
        
        var trimmedProvider = provider.Trim();
        var trimmedModelName = modelName.Trim();
        var trimmedModelVersion = modelVersion.Trim();
        
        if (trimmedProvider.Length > 100 || trimmedModelName.Length > 100 || trimmedModelVersion.Length > 100)
            throw new InvalidAnalysisRunException("AnalysisModelReference fields cannot exceed 100 characters.");
            
        foreach (char c in trimmedProvider + trimmedModelName + trimmedModelVersion)
        {
            if (char.IsControl(c)) throw new InvalidAnalysisRunException("AnalysisModelReference fields cannot contain control characters.");
        }
        
        Provider = trimmedProvider;
        ModelName = trimmedModelName;
        ModelVersion = trimmedModelVersion;
    }

    public bool Equals(AnalysisModelReference? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Provider, other.Provider, StringComparison.Ordinal) && 
               string.Equals(ModelName, other.ModelName, StringComparison.Ordinal) && 
               string.Equals(ModelVersion, other.ModelVersion, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is AnalysisModelReference other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Provider, ModelName, ModelVersion);
}
