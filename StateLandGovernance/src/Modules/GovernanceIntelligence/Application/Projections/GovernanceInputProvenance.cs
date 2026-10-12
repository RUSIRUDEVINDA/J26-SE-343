namespace StateLandGovernance.GovernanceIntelligence.Application.Projections;

/// <summary>
/// Closed vocabulary for the origin classification of an ingested projection or assessment input.
/// </summary>
public enum GovernanceProvenanceKind
{
    RealComponent1 = 1,
    RealComponent3 = 2,
    SyntheticFixture = 3,
    SyntheticPp1Seed = 4
}
/// <summary>
/// Closed vocabulary for the operational or evaluation purpose of an ingested projection or assessment input.
/// </summary>
public enum GovernanceProvenancePurpose
{
    PP1Demonstration = 1,
    AutomatedTest = 2,
    LocalDevelopment = 3,
    ProductionPeerIntegration = 4
}

/// <summary>
/// Explicit provenance contract carried by Component 4 ingested peer projections and persisted assessments.
/// Prevents synthetic demonstration or test fixtures from being silently conflated with real peer-component records.
/// </summary>
public sealed record GovernanceInputProvenance
{
    public const string Pp1SyntheticSeedVersion = "PP1-SYNTH-2026-10-v1";
    public const string FinalPp1SyntheticSeedVersion = "pp1-seed-contract-v2";
    public const string CommissionerAnomalySyntheticSeedVersion = "pp1-anomaly-commissioner-v1";
    public const int MaxIdentifierLength = 128;
    public const int MaxVersionLength = 64;

    public GovernanceInputProvenance(
        string sourceSystem,
        string sourceRecordId,
        string sourceRecordVersion,
        DateTimeOffset sourceObservedAtUtc,
        bool isSynthetic,
        GovernanceProvenanceKind provenanceKind,
        string? syntheticSeedId,
        string? syntheticSeedVersion,
        DateTimeOffset generatedAtUtc,
        GovernanceProvenancePurpose purpose)
    {
        if (string.IsNullOrWhiteSpace(sourceSystem))
        {
            throw new ArgumentException("SourceSystem cannot be null or whitespace.", nameof(sourceSystem));
        }

        if (string.IsNullOrWhiteSpace(sourceRecordId))
        {
            throw new ArgumentException("SourceRecordId cannot be null or whitespace.", nameof(sourceRecordId));
        }

        if (string.IsNullOrWhiteSpace(sourceRecordVersion))
        {
            throw new ArgumentException("SourceRecordVersion cannot be null or whitespace.", nameof(sourceRecordVersion));
        }

        var normalizedSourceSystem = sourceSystem.Trim();
        var normalizedSourceRecordId = sourceRecordId.Trim();
        var normalizedSourceRecordVersion = sourceRecordVersion.Trim();

        if (normalizedSourceSystem.Length > MaxIdentifierLength)
        {
            throw new ArgumentException($"SourceSystem cannot exceed {MaxIdentifierLength} characters.", nameof(sourceSystem));
        }

        if (normalizedSourceRecordId.Length > MaxIdentifierLength)
        {
            throw new ArgumentException($"SourceRecordId cannot exceed {MaxIdentifierLength} characters.", nameof(sourceRecordId));
        }

        if (normalizedSourceRecordVersion.Length > MaxVersionLength)
        {
            throw new ArgumentException($"SourceRecordVersion cannot exceed {MaxVersionLength} characters.", nameof(sourceRecordVersion));
        }

        if (sourceObservedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("SourceObservedAtUtc must have a zero UTC offset.", nameof(sourceObservedAtUtc));
        }

        if (generatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("GeneratedAtUtc must have a zero UTC offset.", nameof(generatedAtUtc));
        }

        if (!Enum.IsDefined(provenanceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(provenanceKind), "ProvenanceKind must be a defined value.");
        }

        if (!Enum.IsDefined(purpose))
        {
            throw new ArgumentOutOfRangeException(nameof(purpose), "Purpose must be a defined value.");
        }

        string? normalizedSeedId = string.IsNullOrWhiteSpace(syntheticSeedId) ? null : syntheticSeedId.Trim();
        string? normalizedSeedVersion = string.IsNullOrWhiteSpace(syntheticSeedVersion) ? null : syntheticSeedVersion.Trim();

        if (isSynthetic)
        {
            if (provenanceKind is not GovernanceProvenanceKind.SyntheticFixture and
                not GovernanceProvenanceKind.SyntheticPp1Seed)
            {
                throw new ArgumentException(
                    "When IsSynthetic is true, ProvenanceKind must identify a synthetic fixture or PP1 seed.",
                    nameof(provenanceKind));
            }

            if (normalizedSeedId is null)
            {
                throw new ArgumentException(
                    "When IsSynthetic is true, SyntheticSeedId is required.",
                    nameof(syntheticSeedId));
            }

            if (normalizedSeedVersion is null)
            {
                throw new ArgumentException(
                    "When IsSynthetic is true, SyntheticSeedVersion is required.",
                    nameof(syntheticSeedVersion));
            }

            if (normalizedSeedId.Length > MaxIdentifierLength)
            {
                throw new ArgumentException(
                    $"SyntheticSeedId cannot exceed {MaxIdentifierLength} characters.",
                    nameof(syntheticSeedId));
            }

            if (normalizedSeedVersion.Length > MaxVersionLength)
            {
                throw new ArgumentException(
                    $"SyntheticSeedVersion cannot exceed {MaxVersionLength} characters.",
                    nameof(syntheticSeedVersion));
            }
        }
        else
        {
            if (provenanceKind is GovernanceProvenanceKind.SyntheticFixture or
                GovernanceProvenanceKind.SyntheticPp1Seed)
            {
                throw new ArgumentException(
                    "When IsSynthetic is false, ProvenanceKind cannot identify synthetic input.",
                    nameof(provenanceKind));
            }

            if (syntheticSeedId is not null)
            {
                throw new ArgumentException(
                    "When IsSynthetic is false, SyntheticSeedId must be null.",
                    nameof(syntheticSeedId));
            }

            if (syntheticSeedVersion is not null)
            {
                throw new ArgumentException(
                    "When IsSynthetic is false, SyntheticSeedVersion must be null.",
                    nameof(syntheticSeedVersion));
            }
        }

        SourceSystem = normalizedSourceSystem;
        SourceRecordId = normalizedSourceRecordId;
        SourceRecordVersion = normalizedSourceRecordVersion;
        SourceObservedAtUtc = sourceObservedAtUtc;
        IsSynthetic = isSynthetic;
        ProvenanceKind = provenanceKind;
        SyntheticSeedId = normalizedSeedId;
        SyntheticSeedVersion = normalizedSeedVersion;
        GeneratedAtUtc = generatedAtUtc;
        Purpose = purpose;
    }

    public string SourceSystem { get; }
    public string SourceRecordId { get; }
    public string SourceRecordVersion { get; }
    public DateTimeOffset SourceObservedAtUtc { get; }
    public bool IsSynthetic { get; }
    public GovernanceProvenanceKind ProvenanceKind { get; }
    public string? SyntheticSeedId { get; }
    public string? SyntheticSeedVersion { get; }
    public DateTimeOffset GeneratedAtUtc { get; }
    public GovernanceProvenancePurpose Purpose { get; }

    /// <summary>
    /// Creates a validated PP1 synthetic demonstration provenance contract.
    /// </summary>
    public static GovernanceInputProvenance CreatePp1Fixture(
        string sourceSystem,
        string sourceRecordId,
        string sourceRecordVersion,
        DateTimeOffset sourceObservedAtUtc,
        string syntheticSeedId,
        DateTimeOffset generatedAtUtc,
        string syntheticSeedVersion = Pp1SyntheticSeedVersion)
    {
        var provenance = new GovernanceInputProvenance(
            sourceSystem: sourceSystem,
            sourceRecordId: sourceRecordId,
            sourceRecordVersion: sourceRecordVersion,
            sourceObservedAtUtc: sourceObservedAtUtc,
            isSynthetic: true,
            provenanceKind: GovernanceProvenanceKind.SyntheticPp1Seed,
            syntheticSeedId: syntheticSeedId,
            syntheticSeedVersion: syntheticSeedVersion,
            generatedAtUtc: generatedAtUtc,
            purpose: GovernanceProvenancePurpose.PP1Demonstration);

        provenance.ValidateForPp1Demonstration();
        return provenance;
    }

    /// <summary>
    /// Validates that this provenance record satisfies the strict PP1 demonstration fixture invariant.
    /// </summary>
    public void ValidateForPp1Demonstration()
    {
        if (!IsSynthetic ||
            ProvenanceKind != GovernanceProvenanceKind.SyntheticPp1Seed ||
            !string.Equals(SyntheticSeedVersion, Pp1SyntheticSeedVersion, StringComparison.Ordinal) &&
            !string.Equals(SyntheticSeedVersion, FinalPp1SyntheticSeedVersion, StringComparison.Ordinal) &&
            !string.Equals(SyntheticSeedVersion, CommissionerAnomalySyntheticSeedVersion, StringComparison.Ordinal) ||
            Purpose != GovernanceProvenancePurpose.PP1Demonstration)
        {
            throw new InvalidOperationException(
                $"PP1 demonstration provenance requires IsSynthetic == true, ProvenanceKind == SyntheticPp1Seed, " +
                $"SyntheticSeedVersion must be an approved PP1 seed contract, and Purpose must equal PP1Demonstration.");
        }
    }
}
