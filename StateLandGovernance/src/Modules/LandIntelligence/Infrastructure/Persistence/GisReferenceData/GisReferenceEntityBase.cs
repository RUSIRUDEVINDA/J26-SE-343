namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData;

/// <summary>
/// Shared provenance metadata for externally imported GIS reference layers.
/// </summary>
public abstract class GisReferenceEntityBase
{
    public const int SourceFeatureIdMaxLength = 256;

    public const int SourceFingerprintMaxLength = 128;

    public Guid Id { get; set; }

    public string SourceName { get; set; } = null!;

    public string SourceLayer { get; set; } = null!;

    /// <summary>
    /// Stable identifier from the external GIS source (for example ArcGIS OBJECTID or GlobalID).
    /// When present, import de-duplication uses SourceName + SourceLayer + SourceFeatureId.
    /// </summary>
    public string? SourceFeatureId { get; set; }

    /// <summary>
    /// Deterministic fallback identity when <see cref="SourceFeatureId"/> is unavailable.
    /// Import de-duplication uses SourceName + SourceLayer + SourceFingerprint in that case.
    /// </summary>
    public string? SourceFingerprint { get; set; }

    public DateTimeOffset ImportedAt { get; set; }
}
