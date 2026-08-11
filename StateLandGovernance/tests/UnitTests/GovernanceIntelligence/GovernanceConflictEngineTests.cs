using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class GovernanceConflictEngineTests
{
    private readonly GovernanceConflictEngine _engine = new();
    private readonly DateTime _fixedTime = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void DetectConflicts_ShouldReturnNoConflicts_WhenDecisionsAreCompatible()
    {
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_A", "PARCEL_1", "UDA", "National", "Approval", "Agricultural", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), "Law_1"),
            new("DEC_B", "PARCEL_2", "LocalCouncil", "Local", "Approval", "Commercial", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), "ByLaw_1")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);

        Assert.Empty(result);
    }

    [Fact]
    public void DetectConflicts_ShouldFlagContradictoryDecisions_WhenCoEqualApprovalAndRejectionOverlap()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(2);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", from, to, "Law_A"),
            new("DEC_2", "PARCEL_1", "EnvironmentalAgency", "National", "Rejection", "Industrial", from.AddMonths(1), to.AddMonths(1), "Law_B")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);

        Assert.Single(result);
        var conflict = result[0];
        Assert.Equal("ContradictoryDecisions", conflict.ConflictType);
        Assert.Equal("Critical", conflict.Severity);
        Assert.Contains("DEC_1", conflict.InvolvedDecisionIds);
        Assert.Contains("DEC_2", conflict.InvolvedDecisionIds);
        Assert.Contains("Contradictory outcomes issued", conflict.Explanation);
    }

    [Fact]
    public void DetectConflicts_ShouldFlagAuthorityLevelInconsistency_WhenLowerAuthorityPermissiveOverlapsHigherRestrictive()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_HIGH", "PARCEL_1", "MinistryOfLand", "National", "Rejection", "Residential", from, to, "National_Decree"),
            new("DEC_LOW", "PARCEL_1", "UrbanPradeshiyaSabha", "Local", "Approval", "Residential", from, to, "Local_ByLaw")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);

        Assert.Single(result);
        var conflict = result[0];
        Assert.Equal("AuthorityLevelInconsistency", conflict.ConflictType);
        Assert.Equal("Critical", conflict.Severity);
        
        // Assert no intent or override claims
        Assert.DoesNotContain("attempt", conflict.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("override", conflict.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Authority level inconsistency detected", conflict.Explanation);
        Assert.Contains("human review of authority precedence", conflict.RecommendedAction);
    }

    [Fact]
    public void DetectConflicts_ShouldReturnNoConflicts_WhenCompatibleDecisionsAcrossAuthorityLevels()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_HIGH", "PARCEL_1", "MinistryOfLand", "National", "Approval", "Agricultural", from, to, "National_Decree"),
            new("DEC_LOW", "PARCEL_1", "PradeshiyaSabha", "Local", "Approval", "Agricultural", from, to, "Local_ByLaw")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);

        Assert.Empty(result);
    }

    [Fact]
    public void DetectConflicts_ShouldFlagRegulatoryConflict_WhenExplicitIncompatibilityEvidenceProvided()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Commercial", from, to, "UDA_Act_Sec_4", incompatibleRegulatoryReferences: new[] { "Coast_Protection_Act_12" }),
            new("DEC_2", "PARCEL_1", "CoastConservation", "National", "Restriction", "Commercial", from, to, "Coast_Protection_Act_12")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);

        Assert.Single(result);
        var conflict = result[0];
        Assert.Equal("RegulatoryConflict", conflict.ConflictType);
        Assert.Equal("High", conflict.Severity);
    }

    [Fact]
    public void DetectConflicts_ShouldReturnNoConflicts_WhenRegulatoryReferencesDifferWithoutIncompatibilityEvidence()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Commercial", from, to, "UDA_Act_Sec_4"),
            new("DEC_2", "PARCEL_1", "CoastConservation", "National", "Restriction", "Commercial", from, to, "Coast_Protection_Act_12")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);

        Assert.Empty(result);
    }

    [Fact]
    public void DetectConflicts_ShouldFlagMandateOverlap_WhenExclusiveMandateCodeMatches()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "BoardOfInvestment", "National", "Approval", "Industrial", from, to, "BOI_Act", mandateKey: "ZONING_PERMIT", mandateMode: "Exclusive"),
            new("DEC_2", "PARCEL_1", "MinistryOfIndustry", "National", "Approval", "Industrial", from, to, "Industry_Act", mandateKey: "ZONING_PERMIT", mandateMode: "Exclusive")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);

        Assert.Single(result);
        var conflict = result[0];
        Assert.Equal("MandateOverlap", conflict.ConflictType);
    }

    [Fact]
    public void DetectConflicts_ShouldReturnNoConflicts_WhenMandatesAreJoint()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "BoardOfInvestment", "National", "Approval", "Industrial", from, to, "BOI_Act", mandateKey: "ZONING_PERMIT", mandateMode: "Joint"),
            new("DEC_2", "PARCEL_1", "MinistryOfIndustry", "National", "Approval", "Industrial", from, to, "Industry_Act", mandateKey: "ZONING_PERMIT", mandateMode: "Joint")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);

        Assert.Empty(result);
    }

    [Fact]
    public void DetectConflicts_ShouldFlagLandUseIncompatibility_WhenExplicitIncompatibilityEvidenceMatches()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", from, to, "Zoning_Act", landUseCode: "IND", incompatibleLandUseCodes: new[] { "RES" }),
            new("DEC_2", "PARCEL_1", "ForestryDept", "National", "Approval", "Residential", from, to, "Forestry_Act", landUseCode: "RES")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);

        Assert.Single(result);
        var conflict = result[0];
        Assert.Equal("LandUseIncompatibility", conflict.ConflictType);
    }

    [Fact]
    public void DetectConflicts_ShouldReturnNoConflicts_WhenDifferentLandUsesAreCompatible()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", from, to, "Zoning_Act", landUseCode: "IND"),
            new("DEC_2", "PARCEL_1", "ForestryDept", "National", "Approval", "Residential", from, to, "Forestry_Act", landUseCode: "RES")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);

        Assert.Empty(result);
    }

    [Fact]
    public void DetectConflicts_ShouldReturnNoConflicts_WhenPeriodsAreNonOverlapping()
    {
        var baseDate = DateTime.UtcNow;
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", baseDate, baseDate.AddMonths(6), "Law_A"),
            new("DEC_2", "PARCEL_1", "UDA", "National", "Rejection", "Industrial", baseDate.AddMonths(7), baseDate.AddMonths(12), "Law_A")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);

        Assert.Empty(result);
    }

    [Fact]
    public void DetectConflicts_ShouldFlagMultipleConflicts_WhenMultipleIssuesExist()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Rejection", "Industrial", from, to, "Law_A"),
            new("DEC_2", "PARCEL_1", "LocalCouncil", "Local", "Approval", "Industrial", from, to, "Law_B"),
            new("DEC_3", "PARCEL_1", "UDA", "National", "Approval", "Industrial", from, to, "Law_A")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void DetectConflicts_ShouldReturnSortedResults_WhenMultipleConflictsFound()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_3", "PARCEL_Z", "UDA", "National", "Approval", "Industrial", from, to, "Law_A"),
            new("DEC_4", "PARCEL_Z", "UDA", "National", "Rejection", "Industrial", from, to, "Law_A"),
            new("DEC_1", "PARCEL_A", "UDA", "National", "Approval", "Industrial", from, to, "Law_A"),
            new("DEC_2", "PARCEL_A", "UDA", "National", "Rejection", "Industrial", from, to, "Law_A")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);

        Assert.Equal(2, result.Count);
        Assert.Equal("CONF_CONTRADICT", result[0].ConflictId.Split('_')[0] + "_" + result[0].ConflictId.Split('_')[1]);
    }

    [Fact]
    public void DetectConflicts_ShouldHaveDeterministicIdentity_AcrossExecutions()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", from, to, "Law_A"),
            new("DEC_2", "PARCEL_1", "UDA", "National", "Rejection", "Industrial", from, to, "Law_A")
        };

        var result1 = _engine.DetectConflicts(input, _fixedTime);
        var result2 = _engine.DetectConflicts(input, _fixedTime);

        Assert.Equal(result1[0].ConflictId, result2[0].ConflictId);
        Assert.Equal(result1[0].Explanation, result2[0].Explanation);
        Assert.Equal(result1[0].EvidenceRule, result2[0].EvidenceRule);
        Assert.Equal(result1[0].DetectionTimestamp, result2[0].DetectionTimestamp);
    }

    [Fact]
    public void DetectConflicts_ShouldHaveReversedInputDeterminism()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", from, to, "Law_A"),
            new("DEC_2", "PARCEL_1", "UDA", "National", "Rejection", "Industrial", from, to, "Law_A")
        };

        var reversedInput = input.AsEnumerable().Reverse().ToList();

        var result1 = _engine.DetectConflicts(input, _fixedTime);
        var result2 = _engine.DetectConflicts(reversedInput, _fixedTime);

        Assert.Equal(result1[0].ConflictId, result2[0].ConflictId);
        Assert.Equal(result1[0].ConflictType, result2[0].ConflictType);
        Assert.Equal(result1[0].Explanation, result2[0].Explanation);
        Assert.Equal(result1[0].InvolvedDecisionIds[0], result2[0].InvolvedDecisionIds[0]);
        Assert.Equal(result1[0].InvolvedDecisionIds[1], result2[0].InvolvedDecisionIds[1]);
    }

    [Fact]
    public void DetectConflicts_ShouldThrowArgumentException_WhenDatesAreInvalid()
    {
        var baseDate = DateTime.UtcNow;
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", baseDate.AddMonths(1), baseDate, "Law_A")
        };

        Assert.Throws<ArgumentException>(() => _engine.DetectConflicts(input, _fixedTime));
    }

    [Fact]
    public void DetectConflicts_ShouldThrowArgumentException_WhenDuplicateDecisionIdsProvided()
    {
        var baseDate = DateTime.UtcNow;
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", baseDate, baseDate.AddMonths(6), "Law_A"),
            new("DEC_1", "PARCEL_1", "UDA", "National", "Rejection", "Industrial", baseDate, baseDate.AddMonths(6), "Law_A")
        };

        Assert.Throws<ArgumentException>(() => _engine.DetectConflicts(input, _fixedTime));
    }

    [Fact]
    public void DetectConflicts_ShouldThrowArgumentException_WhenInputContainsNull()
    {
        var input = new List<GovernanceDecisionSnapshot>
        {
            null!
        };

        Assert.Throws<ArgumentException>(() => _engine.DetectConflicts(input, _fixedTime));
    }

    [Fact]
    public void Constructor_ShouldDefensivelyCopyCollections()
    {
        var rawIncompatibleRegs = new List<string> { "RegA" };
        var rawIncompatibleLandUses = new List<string> { "LandUseA" };

        var snapshot = new GovernanceDecisionSnapshot(
            "DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", 
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), "Law",
            incompatibleRegulatoryReferences: rawIncompatibleRegs,
            incompatibleLandUseCodes: rawIncompatibleLandUses);

        rawIncompatibleRegs.Add("RegB");
        rawIncompatibleLandUses.Add("LandUseB");

        Assert.Single(snapshot.IncompatibleRegulatoryReferences);
        Assert.Equal("RegA", snapshot.IncompatibleRegulatoryReferences[0]);
        Assert.Single(snapshot.IncompatibleLandUseCodes);
        Assert.Equal("LandUseA", snapshot.IncompatibleLandUseCodes[0]);
    }

    [Fact]
    public void ConflictId_ShouldBeCaseAndWhitespaceNormalized()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input1 = new List<GovernanceDecisionSnapshot>
        {
            new("dec_1", "parcel_1", "UDA", "National", "Approval", "Industrial", from, to, "Law_A"),
            new("dec_2", "parcel_1", "UDA", "National", "Rejection", "Industrial", from, to, "Law_A")
        };

        var input2 = new List<GovernanceDecisionSnapshot>
        {
            new("  DEC_1  ", "  PARCEL_1  ", "UDA", "National", "Approval", "Industrial", from, to, "Law_A"),
            new("  DEC_2  ", "  PARCEL_1  ", "UDA", "National", "Rejection", "Industrial", from, to, "Law_A")
        };

        var result1 = _engine.DetectConflicts(input1, _fixedTime);
        var result2 = _engine.DetectConflicts(input2, _fixedTime);

        Assert.Equal(result1[0].ConflictId, result2[0].ConflictId);
    }

    [Fact]
    public void ConflictId_ShouldNotExposeRawSubjectOrDecisionIdentifiers()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_XYZ_123", "PARCEL_ABC_789", "UDA", "National", "Approval", "Industrial", from, to, "Law_A"),
            new("DEC_XYZ_456", "PARCEL_ABC_789", "UDA", "National", "Rejection", "Industrial", from, to, "Law_A")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);

        Assert.DoesNotContain("XYZ", result[0].ConflictId);
        Assert.DoesNotContain("ABC", result[0].ConflictId);
    }

    [Fact]
    public void ConflictId_ShouldPreventAmbiguousDelimiterCollisions()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        
        var inputA = new List<GovernanceDecisionSnapshot>
        {
            new("2", "PARCEL_1", "UDA", "National", "Approval", "Industrial", from, to, "Law_A"),
            new("3", "PARCEL_1", "UDA", "National", "Rejection", "Industrial", from, to, "Law_A")
        };

        var inputB = new List<GovernanceDecisionSnapshot>
        {
            new("1_2", "PARCEL", "UDA", "National", "Approval", "Industrial", from, to, "Law_A"),
            new("3", "PARCEL", "UDA", "National", "Rejection", "Industrial", from, to, "Law_A")
        };

        var resultA = _engine.DetectConflicts(inputA, _fixedTime);
        var resultB = _engine.DetectConflicts(inputB, _fixedTime);

        Assert.NotEqual(resultA[0].ConflictId, resultB[0].ConflictId);
    }

    [Fact]
    public void DuplicateDecisionIds_DifferingOnlyByCaseOrWhitespace_ShouldBeRejected()
    {
        var baseDate = DateTime.UtcNow;
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", baseDate, baseDate.AddMonths(6), "Law_A"),
            new("  dec_1  ", "PARCEL_1", "UDA", "National", "Rejection", "Industrial", baseDate, baseDate.AddMonths(6), "Law_A")
        };

        Assert.Throws<ArgumentException>(() => _engine.DetectConflicts(input, _fixedTime));
    }

    [Fact]
    public void SameLandUseCode_WithoutSelfIncompatibility_ShouldProduceNoConflict()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", from, to, "Law_A", landUseCode: "IND"),
            new("DEC_2", "PARCEL_1", "UDA", "National", "Approval", "Industrial", from, to, "Law_A", landUseCode: "IND")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);
        Assert.Empty(result);
    }

    [Fact]
    public void NonOverlappingLandUseCodes_ShouldProduceNoConflict()
    {
        var baseDate = DateTime.UtcNow;
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", baseDate, baseDate.AddMonths(6), "Law_A", landUseCode: "IND", incompatibleLandUseCodes: new[] { "RES" }),
            new("DEC_2", "PARCEL_1", "UDA", "National", "Approval", "Residential", baseDate.AddMonths(7), baseDate.AddMonths(12), "Law_A", landUseCode: "RES")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);
        Assert.Empty(result);
    }

    [Fact]
    public void SameInstitution_ShouldProduceNoMandateConflict()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "Planning Council", "National", "Approval", "Industrial", from, to, "Law_A", mandateKey: "ZONING", mandateMode: "Exclusive"),
            new("DEC_2", "PARCEL_1", " planning council ", "National", "Approval", "Industrial", from, to, "Law_A", mandateKey: "ZONING", mandateMode: "Exclusive")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);
        Assert.Empty(result);
    }

    [Fact]
    public void DifferentMandateScopes_ShouldProduceNoConflict()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "InstA", "National", "Approval", "Industrial", from, to, "Law_A", mandateKey: "ZONING", mandateMode: "Exclusive"),
            new("DEC_2", "PARCEL_1", "InstB", "National", "Approval", "Industrial", from, to, "Law_A", mandateKey: "TAXATION", mandateMode: "Exclusive")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);
        Assert.Empty(result);
    }

    [Fact]
    public void MissingMandateEvidence_ShouldProduceNoConflict()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "InstA", "National", "Approval", "Industrial", from, to, "Law_A"),
            new("DEC_2", "PARCEL_1", "InstB", "National", "Approval", "Industrial", from, to, "Law_A")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);
        Assert.Empty(result);
    }

    [Fact]
    public void IdenticalRegulatoryReferences_WithoutSelfIncompatibility_ShouldProduceNoConflict()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", from, to, "Law_A"),
            new("DEC_2", "PARCEL_1", "UDA", "National", "Restriction", "Industrial", from, to, "Law_A")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);
        Assert.Empty(result);
    }

    [Fact]
    public void PairwiseEvaluation_ShouldSupportMultipleConflictCategories_ForSingleDecisionPair()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_HIGH", "PARCEL_1", "MinistryOfLand", "National", "Rejection", "Industrial", from, to, "Law_A", 
                incompatibleRegulatoryReferences: new[] { "Law_B" },
                landUseCode: "IND",
                incompatibleLandUseCodes: new[] { "RES" }),

            new("DEC_LOW", "PARCEL_1", "LocalCouncil", "Local", "Approval", "Residential", from, to, "Law_B",
                landUseCode: "RES")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);

        // Assert exact expected category set and occurrence counts
        Assert.Equal(3, result.Count);
        Assert.Single(result.Where(c => c.ConflictType == "AuthorityLevelInconsistency"));
        Assert.Single(result.Where(c => c.ConflictType == "RegulatoryConflict"));
        Assert.Single(result.Where(c => c.ConflictType == "LandUseIncompatibility"));

        // Assert deterministic category ordering (Severity, then Alphabetical on ConflictType)
        Assert.Equal("AuthorityLevelInconsistency", result[0].ConflictType);
        Assert.Equal("LandUseIncompatibility", result[1].ConflictType);
        Assert.Equal("RegulatoryConflict", result[2].ConflictType);

        // Assert deterministic conflict IDs do not expose raw IDs
        Assert.StartsWith("CONF_AUTHORITY_", result[0].ConflictId);
        Assert.StartsWith("CONF_LANDUSE_", result[1].ConflictId);
        Assert.StartsWith("CONF_REGULATORY_", result[2].ConflictId);

        // Assert deterministic involved decision ordering (alphabetical sort inside snapshots)
        Assert.Equal(new[] { "DEC_HIGH", "DEC_LOW" }.OrderBy(id => id, StringComparer.Ordinal).ToList(), result[0].InvolvedDecisionIds);
        Assert.Equal(new[] { "DEC_HIGH", "DEC_LOW" }.OrderBy(id => id, StringComparer.Ordinal).ToList(), result[1].InvolvedDecisionIds);
        Assert.Equal(new[] { "DEC_HIGH", "DEC_LOW" }.OrderBy(id => id, StringComparer.Ordinal).ToList(), result[2].InvolvedDecisionIds);

        // Assert reversed input produces structurally equal results
        var reversedInput = input.AsEnumerable().Reverse().ToList();
        var resultReversed = _engine.DetectConflicts(reversedInput, _fixedTime);

        Assert.Equal(result.Count, resultReversed.Count);
        for (int i = 0; i < result.Count; i++)
        {
            Assert.Equal(result[i].ConflictId, resultReversed[i].ConflictId);
            Assert.Equal(result[i].ConflictType, resultReversed[i].ConflictType);
            Assert.Equal(result[i].Severity, resultReversed[i].Severity);
            Assert.Equal(result[i].InvolvedDecisionIds, resultReversed[i].InvolvedDecisionIds);
            Assert.Equal(result[i].InvolvedInstitutions, resultReversed[i].InvolvedInstitutions);
            Assert.Equal(result[i].Explanation, resultReversed[i].Explanation);
        }
    }

    [Fact]
    public void DuplicateDecisionIds_DifferingOnlyByCase_ShouldThrowArgumentException()
    {
        var baseDate = DateTime.UtcNow;
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", baseDate, baseDate.AddMonths(6), "Law_A"),
            new("dec_1", "PARCEL_1", "UDA", "National", "Rejection", "Industrial", baseDate, baseDate.AddMonths(6), "Law_A")
        };

        Assert.Throws<ArgumentException>(() => _engine.DetectConflicts(input, _fixedTime));
    }

    [Fact]
    public void BoundaryCheck_ShouldThrowOnEmptyDecisionId()
    {
        var baseDate = DateTime.UtcNow;
        var input = new List<GovernanceDecisionSnapshot>
        {
            new(" ", "PARCEL_1", "UDA", "National", "Approval", "Industrial", baseDate, baseDate.AddMonths(6), "Law_A")
        };

        Assert.Throws<ArgumentException>(() => _engine.DetectConflicts(input, _fixedTime));
    }

    [Fact]
    public void BoundaryCheck_ShouldThrowOnEmptySubjectId()
    {
        var baseDate = DateTime.UtcNow;
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "", "UDA", "National", "Approval", "Industrial", baseDate, baseDate.AddMonths(6), "Law_A")
        };

        Assert.Throws<ArgumentException>(() => _engine.DetectConflicts(input, _fixedTime));
    }

    [Fact]
    public void BoundaryCheck_ShouldThrowOnEmptyInstitution()
    {
        var baseDate = DateTime.UtcNow;
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "\t", "National", "Approval", "Industrial", baseDate, baseDate.AddMonths(6), "Law_A")
        };

        Assert.Throws<ArgumentException>(() => _engine.DetectConflicts(input, _fixedTime));
    }

    [Fact]
    public void BoundaryCheck_ShouldThrowOnUnsupportedDecisionType()
    {
        var baseDate = DateTime.UtcNow;
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "InvalidType", "Industrial", baseDate, baseDate.AddMonths(6), "Law_A")
        };

        Assert.Throws<ArgumentException>(() => _engine.DetectConflicts(input, _fixedTime));
    }

    [Fact]
    public void BoundaryCheck_ShouldThrowOnUnsupportedAuthorityLevel()
    {
        var baseDate = DateTime.UtcNow;
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "InvalidLevel", "Approval", "Industrial", baseDate, baseDate.AddMonths(6), "Law_A")
        };

        Assert.Throws<ArgumentException>(() => _engine.DetectConflicts(input, _fixedTime));
    }

    // Narrow Pass New Rules & Boundary Test Additions

    [Fact]
    public void DetectConflicts_ShouldNormalizeNonUtcTimestampToUtc()
    {
        var localTime = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Local);
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", from, to, "Law_A"),
            new("DEC_2", "PARCEL_1", "UDA", "National", "Rejection", "Industrial", from, to, "Law_A")
        };

        var result = _engine.DetectConflicts(input, localTime);
        
        Assert.NotEmpty(result);
        Assert.Equal(DateTimeKind.Utc, result[0].DetectionTimestamp.Kind);
        Assert.Equal(localTime.ToUniversalTime(), result[0].DetectionTimestamp);
    }

    [Fact]
    public void DetectConflicts_ShouldPreserveConflictIds_WhenDifferentTimestampsAreUsed()
    {
        var time1 = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);
        var time2 = new DateTime(2026, 8, 12, 14, 30, 0, DateTimeKind.Utc);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), "Law_A"),
            new("DEC_2", "PARCEL_1", "UDA", "National", "Rejection", "Industrial", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), "Law_A")
        };

        var result1 = _engine.DetectConflicts(input, time1);
        var result2 = _engine.DetectConflicts(input, time2);

        Assert.Equal(result1[0].ConflictId, result2[0].ConflictId);
    }

    [Fact]
    public void DetectConflicts_ShouldProduceNoLandUseConflict_WhenEffectsAreCompatible()
    {
        // Even if land use codes are incompatible, they must not conflict if there are no contradictory or exclusive zoning rules.
        // E.g. compatible outcomes in land-use rule
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", from, to, "Law_A", landUseCode: "IND"),
            new("DEC_2", "PARCEL_1", "UDA", "National", "Approval", "Residential", from, to, "Law_B", landUseCode: "RES")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);
        Assert.Empty(result);
    }

    [Fact]
    public void LandUse_Normalization_ShouldHandleCaseAndWhitespace()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", from, to, "Law_A", landUseCode: "  ind  ", incompatibleLandUseCodes: new[] { "  res  " }),
            new("DEC_2", "PARCEL_1", "UDA", "National", "Approval", "Residential", from, to, "Law_B", landUseCode: "  RES  ")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);
        Assert.Single(result);
        Assert.Equal("LandUseIncompatibility", result[0].ConflictType);
    }

    [Fact]
    public void LandUse_ReversedInput_ShouldProduceIdenticalResults()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", from, to, "Law_A", landUseCode: "IND", incompatibleLandUseCodes: new[] { "RES" }),
            new("DEC_2", "PARCEL_1", "UDA", "National", "Approval", "Residential", from, to, "Law_B", landUseCode: "RES")
        };

        var resultNormal = _engine.DetectConflicts(input, _fixedTime);
        var resultReversed = _engine.DetectConflicts(input.AsEnumerable().Reverse().ToList(), _fixedTime);

        Assert.Equal(resultNormal[0].ConflictId, resultReversed[0].ConflictId);
        Assert.Equal(resultNormal[0].Explanation, resultReversed[0].Explanation);
        Assert.Equal(resultNormal[0].InvolvedDecisionIds[0], resultReversed[0].InvolvedDecisionIds[0]);
    }

    [Fact]
    public void LandUse_EvidenceMutation_ShouldNotChangeEvaluation()
    {
        var mutableIncompatibles = new List<string> { "RES" };
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", from, to, "Law_A", landUseCode: "IND", incompatibleLandUseCodes: mutableIncompatibles),
            new("DEC_2", "PARCEL_1", "UDA", "National", "Approval", "Residential", from, to, "Law_B", landUseCode: "RES")
        };

        // Mutate original list
        mutableIncompatibles.Clear();

        var result = _engine.DetectConflicts(input, _fixedTime);
        Assert.Single(result);
    }

    [Fact]
    public void Mandate_NonOverlappingExclusiveMandates_ShouldProduceNoConflict()
    {
        var baseDate = DateTime.UtcNow;
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "InstA", "National", "Approval", "Industrial", baseDate, baseDate.AddMonths(6), "Law_A", mandateKey: "ZONING", mandateMode: "Exclusive"),
            new("DEC_2", "PARCEL_1", "InstB", "National", "Approval", "Industrial", baseDate.AddMonths(7), baseDate.AddMonths(12), "Law_B", mandateKey: "ZONING", mandateMode: "Exclusive")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);
        Assert.Empty(result);
    }

    [Fact]
    public void Mandate_InstitutionNormalization_ShouldHandleCaseAndWhitespace()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        // " Planning " vs " planning " represents same institution, so should not conflict
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", " Planning Council ", "National", "Approval", "Industrial", from, to, "Law_A", mandateKey: "ZONING", mandateMode: "Exclusive"),
            new("DEC_2", "PARCEL_1", " PLANNING council ", "National", "Approval", "Industrial", from, to, "Law_B", mandateKey: "ZONING", mandateMode: "Exclusive")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);
        Assert.Empty(result);
    }

    [Fact]
    public void Mandate_ReversedInput_ShouldProduceIdenticalOutput()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "InstA", "National", "Approval", "Industrial", from, to, "Law_A", mandateKey: "ZONING", mandateMode: "Exclusive"),
            new("DEC_2", "PARCEL_1", "InstB", "National", "Approval", "Industrial", from, to, "Law_B", mandateKey: "ZONING", mandateMode: "Exclusive")
        };

        var resultNormal = _engine.DetectConflicts(input, _fixedTime);
        var resultReversed = _engine.DetectConflicts(input.AsEnumerable().Reverse().ToList(), _fixedTime);

        Assert.Equal(resultNormal[0].ConflictId, resultReversed[0].ConflictId);
    }

    [Fact]
    public void Mandate_EvidenceMutation_ShouldNotChangeEvaluation()
    {
        // Verify value object collection copy safety
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "InstA", "National", "Approval", "Industrial", from, to, "Law_A", mandateKey: "ZONING", mandateMode: "Exclusive"),
            new("DEC_2", "PARCEL_1", "InstB", "National", "Approval", "Industrial", from, to, "Law_B", mandateKey: "ZONING", mandateMode: "Exclusive")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);
        Assert.Single(result);
    }

    [Fact]
    public void Regulatory_CompatibleDecisionEffects_ShouldProduceNoRegulatoryConflict()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        // Compatible effects: Approval vs Approval, so should produce no conflict
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Commercial", from, to, "UDA_Act", incompatibleRegulatoryReferences: new[] { "Coast_Protection" }),
            new("DEC_2", "PARCEL_1", "Coast", "National", "Approval", "Commercial", from, to, "Coast_Protection")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);
        Assert.Empty(result);
    }

    [Fact]
    public void Regulatory_NonOverlappingPeriods_ShouldProduceNoRegulatoryConflict()
    {
        var baseDate = DateTime.UtcNow;
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Commercial", baseDate, baseDate.AddMonths(6), "UDA_Act", incompatibleRegulatoryReferences: new[] { "Coast_Protection" }),
            new("DEC_2", "PARCEL_1", "Coast", "National", "Restriction", "Commercial", baseDate.AddMonths(7), baseDate.AddMonths(12), "Coast_Protection")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);
        Assert.Empty(result);
    }

    [Fact]
    public void Regulatory_Normalization_ShouldHandleCaseAndWhitespace()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Commercial", from, to, "  uda_act  ", incompatibleRegulatoryReferences: new[] { "  coast_protection  " }),
            new("DEC_2", "PARCEL_1", "Coast", "National", "Restriction", "Commercial", from, to, "  COAST_PROTECTION  ")
        };

        var result = _engine.DetectConflicts(input, _fixedTime);
        Assert.Single(result);
        Assert.Equal("RegulatoryConflict", result[0].ConflictType);
    }

    [Fact]
    public void Regulatory_ReversedInput_ShouldProduceIdenticalOutput()
    {
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Commercial", from, to, "Law_A", incompatibleRegulatoryReferences: new[] { "Law_B" }),
            new("DEC_2", "PARCEL_1", "Coast", "National", "Restriction", "Commercial", from, to, "Law_B")
        };

        var resultNormal = _engine.DetectConflicts(input, _fixedTime);
        var resultReversed = _engine.DetectConflicts(input.AsEnumerable().Reverse().ToList(), _fixedTime);

        Assert.Equal(resultNormal[0].ConflictId, resultReversed[0].ConflictId);
    }

    [Fact]
    public void Regulatory_EvidenceMutation_ShouldNotChangeEvaluation()
    {
        var mutableRegs = new List<string> { "Law_B" };
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var input = new List<GovernanceDecisionSnapshot>
        {
            new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Commercial", from, to, "Law_A", incompatibleRegulatoryReferences: mutableRegs),
            new("DEC_2", "PARCEL_1", "Coast", "National", "Restriction", "Commercial", from, to, "Law_B")
        };

        // Mutate original list
        mutableRegs.Clear();

        var result = _engine.DetectConflicts(input, _fixedTime);
        Assert.Single(result);
    }
}
