using System;
using System.Collections.Generic;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Mappings;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class StructuredEvaluationMappingTests
{
    [Fact]
    public void RiskEvaluationMapper_DropsUnsanitizedEvidenceSummaryForPrivacy()
    {
        var auditRecord = GovernanceAuditRecord.Create(EngineType.RiskAndCorruption, "AssessRisk", "ElevatedRiskDetected", "Risk evaluation details");
        var indicator = new GovernanceRiskIndicator(
            "IND-01",
            GovernanceRiskCategory.ApprovalPatternAnomaly,
            25,
            GovernanceRiskSeverity.High,
            "SUBJ-123",
            new[] { "ROLE_APPROVER" },
            "RULE-PROC",
            "Process anomaly detected",
            "Human review required",
            "UNSANITIZED FREE TEXT OFFICER PII REMARKS THAT MUST BE DROPPED");

        var result = new GovernanceRiskAssessmentResult(
            "SUBJ-123",
            25,
            GovernanceRiskSeverity.High,
            new[] { indicator },
            DateTime.UtcNow,
            true);

        var (_, evalEntity) = RiskEvaluationMapper.MapToEntities(auditRecord, result);

        Assert.NotNull(evalEntity);
        Assert.Single(evalEntity.RiskIndicators);
        var mappedIndicator = evalEntity.RiskIndicators[0];

        Assert.Equal("IND-01", mappedIndicator.IndicatorId);
        Assert.Equal("ApprovalPatternAnomaly", mappedIndicator.Category);
        Assert.Equal("High", mappedIndicator.Severity);
        Assert.Contains("ROLE_APPROVER", mappedIndicator.InvolvedActorsJson);
    }

    [Fact]
    public void GovernanceExplanationMapper_PreservesOrderIndexAndEnumStrings()
    {
        var auditRecord = GovernanceAuditRecord.Create(EngineType.ExplainableGovernance, "ExplainGov", "Generated", "Details");
        var item1 = new GovernanceExplanationItem(
            "ITEM-01",
            EngineType.RegulatoryCompliance,
            "Compliant",
            GovernanceExplanationSeverity.Low,
            "REASON-01",
            "Title 1",
            "Explanation 1",
            new[] { "Raw evidence line 1" },
            "Action 1",
            false);

        var item2 = new GovernanceExplanationItem(
            "ITEM-02",
            EngineType.GovernanceConflict,
            "ConflictDetected",
            GovernanceExplanationSeverity.High,
            "REASON-02",
            "Title 2",
            "Explanation 2",
            new[] { "Raw evidence line 2" },
            "Action 2",
            true);

        var result = new GovernanceExplanationResult(
            "EXP-100",
            "SUBJ-999",
            GovernanceExplanationSeverity.High,
            true,
            new[] { item1, item2 },
            DateTime.UtcNow,
            "Disclaimer text");

        var (_, evalEntity) = GovernanceExplanationMapper.MapToEntities(auditRecord, result);

        Assert.Equal(2, evalEntity.Items.Count);
        Assert.Equal(0, evalEntity.Items[0].OrderIndex);
        Assert.Equal("RegulatoryCompliance", evalEntity.Items[0].SourceEngine);
        Assert.Equal("Low", evalEntity.Items[0].Severity);

        Assert.Equal(1, evalEntity.Items[1].OrderIndex);
        Assert.Equal("GovernanceConflict", evalEntity.Items[1].SourceEngine);
        Assert.Equal("High", evalEntity.Items[1].Severity);
    }

    [Fact]
    public void ConsensusEvaluationMapper_OmitsSummaryNotesFromPositionsForPrivacy()
    {
        var auditRecord = GovernanceAuditRecord.Create(EngineType.Consensus, "EvaluateConsensus", "Approved", "Consensus details");
        var position = new InstitutionalGovernancePosition(
            "INST-LRA",
            InstitutionalPositionType.Approve,
            "Role_Head",
            "REASON-PASS",
            "SENSITIVE PRIVATE COMMENT FROM OFFICER",
            DateTime.UtcNow);

        var result = new GovernanceConsensusResult(
            "EVAL-CONS-1",
            "SUBJ-777",
            ConsensusOutcome.ConsensusReached,
            1, 1, 1, 0, 1, 0, 0, 0, 0,
            true, true, true, 0,
            "Approved summary",
            "Proceed with lease",
            DateTime.UtcNow);

        var (_, evalEntity) = ConsensusEvaluationMapper.MapToEntities(auditRecord, result, new[] { position });

        Assert.Single(evalEntity.InstitutionPositions);
        var mappedPos = evalEntity.InstitutionPositions[0];

        Assert.Equal("INST-LRA", mappedPos.InstitutionId);
        Assert.Equal("Approve", mappedPos.Position);
        Assert.Equal("Role_Head", mappedPos.AuthorityRole);
        Assert.Equal("REASON-PASS", mappedPos.ReasonCode);
    }
}
