using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Application.Commands;

/// <summary>
/// Command to evaluate external decision snapshots and detect potential regulatory or mandate conflicts.
/// </summary>
public sealed record DetectConflictsCommand(
    string ActionName,
    IReadOnlyList<GovernanceDecisionSnapshotDto> Decisions
);

/// <summary>
/// Handler for the DetectConflictsCommand.
/// </summary>
public sealed class DetectConflictsCommandHandler
{
    private readonly IGovernanceConflictEngine _conflictEngine;
    private readonly IGovernanceAuditRepository _auditRepository;
    private readonly TimeProvider _timeProvider;

    public DetectConflictsCommandHandler(
        IGovernanceConflictEngine conflictEngine,
        IGovernanceAuditRepository auditRepository,
        TimeProvider timeProvider)
    {
        _conflictEngine = conflictEngine;
        _auditRepository = auditRepository;
        _timeProvider = timeProvider;
    }

    public async Task<ConflictDetectionResultDto> HandleAsync(DetectConflictsCommand command, CancellationToken cancellationToken = default)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command), "Command details cannot be null.");
        }

        if (command.Decisions is null)
        {
            throw new ArgumentException("Decisions collection cannot be null.");
        }

        // Obtain a single evaluation timestamp using injected TimeProvider as a stable source
        var evaluationTimestamp = _timeProvider.GetUtcNow().UtcDateTime;

        // 1. Map DTO inputs to domain value objects (handling the extended evidence collections)
        var snapshots = new List<GovernanceDecisionSnapshot>();
        foreach (var d in command.Decisions)
        {
            if (d is null)
            {
                throw new ArgumentException("Decisions collection contains null elements.");
            }

            snapshots.Add(new GovernanceDecisionSnapshot(
                decisionId: d.DecisionId,
                subjectId: d.SubjectId,
                institutionName: d.InstitutionName,
                authorityLevel: d.AuthorityLevel,
                decisionType: d.DecisionType,
                proposedUse: d.ProposedUse,
                effectiveFrom: d.EffectiveFrom,
                effectiveTo: d.EffectiveTo,
                regulatoryReference: d.RegulatoryReference,
                incompatibleRegulatoryReferences: d.IncompatibleRegulatoryReferences,
                mandateKey: d.MandateKey,
                mandateMode: d.MandateMode,
                landUseCode: d.LandUseCode,
                incompatibleLandUseCodes: d.IncompatibleLandUseCodes));
        }

        // 2. Evaluate conflicts using the domain engine with the unified timestamp
        var detectedConflicts = _conflictEngine.DetectConflicts(snapshots, evaluationTimestamp);

        // Calculate highest severity metadata
        var highestSeverity = "None";
        if (detectedConflicts.Count > 0)
        {
            if (detectedConflicts.Any(c => string.Equals(c.Severity, "Critical", StringComparison.OrdinalIgnoreCase)))
            {
                highestSeverity = "Critical";
            }
            else if (detectedConflicts.Any(c => string.Equals(c.Severity, "High", StringComparison.OrdinalIgnoreCase)))
            {
                highestSeverity = "High";
            }
            else if (detectedConflicts.Any(c => string.Equals(c.Severity, "Medium", StringComparison.OrdinalIgnoreCase)))
            {
                highestSeverity = "Medium";
            }
            else
            {
                highestSeverity = detectedConflicts[0].Severity;
            }
        }

        // 3. Record audit trail using metadata only
        var status = detectedConflicts.Count > 0 ? "ConflictsDetected" : "NoConflicts";
        var auditDetails = $"Evaluation occurred. Evaluated {snapshots.Count} decisions. Detected conflicts: {detectedConflicts.Count}. Highest severity: {highestSeverity}.";
        
        var auditRecord = GovernanceAuditRecord.Create(
            EngineType.GovernanceConflict,
            command.ActionName,
            status,
            auditDetails,
            evaluationTimestamp);

        await _auditRepository.AddAsync(auditRecord, cancellationToken);

        // 4. Map domain results back to DTOs
        var conflictDtos = detectedConflicts.Select(c => new DetectedConflictDto(
            c.ConflictId,
            c.ConflictType,
            c.Severity,
            c.DetectionStatus,
            c.InvolvedDecisionIds,
            c.InvolvedInstitutions,
            c.SubjectId,
            c.Explanation,
            c.EvidenceRule,
            c.RecommendedAction,
            c.DetectionTimestamp)).ToList();

        return new ConflictDetectionResultDto(status, conflictDtos);
    }
}
