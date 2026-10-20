namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public sealed record ProjectLeaseReadyDesignationRecord(
    string ResearchRecordId,
    Guid LandParcelId,
    string DatasetKey);

public sealed record ProjectLeaseReadyDesignationDto(
    string DatasetKey,
    string ResearchRecordId,
    string DesignationLabel,
    string DesignationNotice,
    bool IsDesignatedInProjectDataset,
    bool IsEffectiveForLeaseFlow,
    bool ExcludedBecauseSourceReportedPrivate);

public sealed record LandParcelMetricsDto(
    int TotalParcelCount,
    int ProjectDesignatedParcelsCount,
    int ProjectDesignatedParcelsEffectiveCount);
