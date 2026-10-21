namespace StateLandGovernance.UnitTests.WorkflowGovernance.Integrations;

using System;
using System.Collections.Generic;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Domain.Screening;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Mappers;
using Xunit;

public class NormalizationAndMappingTests
{
    // ==========================================
    // 1. District -> Province Mapping Tests
    // ==========================================

    [Theory]
    [InlineData("Colombo", "Western")]
    [InlineData("Gampaha", "Western")]
    [InlineData("Kalutara", "Western")]
    [InlineData("Kandy", "Central")]
    [InlineData("Matale", "Central")]
    [InlineData("Nuwara Eliya", "Central")]
    [InlineData("Galle", "Southern")]
    [InlineData("Matara", "Southern")]
    [InlineData("Hambantota", "Southern")]
    [InlineData("Jaffna", "Northern")]
    [InlineData("Kilinochchi", "Northern")]
    [InlineData("Mannar", "Northern")]
    [InlineData("Vavuniya", "Northern")]
    [InlineData("Mullaitivu", "Northern")]
    [InlineData("Batticaloa", "Eastern")]
    [InlineData("Ampara", "Eastern")]
    [InlineData("Trincomalee", "Eastern")]
    [InlineData("Kurunegala", "North Western")]
    [InlineData("Puttalam", "North Western")]
    [InlineData("Anuradhapura", "North Central")]
    [InlineData("Polonnaruwa", "North Central")]
    [InlineData("Badulla", "Uva")]
    [InlineData("Monaragala", "Uva")]
    [InlineData("Moneragala", "Uva")]
    [InlineData("Ratnapura", "Sabaragamuwa")]
    [InlineData("Kegalle", "Sabaragamuwa")]
    public void DistrictToProvince_ShouldReturnCorrectProvince_ForKnownDistricts(string district, string expectedProvince)
    {
        var province = DistrictToProvinceMapper.TryMapDistrictToProvince(district);
        Assert.Equal(expectedProvince, province);
    }

    [Theory]
    [InlineData("colombo", "Western")]
    [InlineData("  HAMBANTOTA  ", "Southern")]
    [InlineData("kandy", "Central")]
    public void DistrictToProvince_ShouldBeCaseAndWhitespaceInsensitive(string district, string expectedProvince)
    {
        var province = DistrictToProvinceMapper.TryMapDistrictToProvince(district);
        Assert.Equal(expectedProvince, province);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Atlantis")]
    [InlineData("Mars")]
    [InlineData("UnknownDistrict")]
    public void DistrictToProvince_ShouldReturnNull_ForUnknownOrEmptyDistrict(string? district)
    {
        var province = DistrictToProvinceMapper.TryMapDistrictToProvince(district);
        Assert.Null(province);
    }

    // ==========================================
    // 2. Land Extent Normalization Tests
    // ==========================================

    [Fact]
    public void ExtentNormalizer_ShouldConvertAcresToHectares()
    {
        // 10 acres * 0.40468564 = ~4.0469 ha
        var result = LandExtentNormalizer.TryNormalizeToHectares("10 acres");
        Assert.NotNull(result);
        Assert.Equal(4.0469m, result.Value);

        var separateUnit = LandExtentNormalizer.TryNormalizeToHectares("10", "acres");
        Assert.NotNull(separateUnit);
        Assert.Equal(4.0469m, separateUnit.Value);
    }

    [Fact]
    public void ExtentNormalizer_ShouldPassThroughHectares()
    {
        var result = LandExtentNormalizer.TryNormalizeToHectares("5 hectares");
        Assert.NotNull(result);
        Assert.Equal(5.0000m, result.Value);

        var haShort = LandExtentNormalizer.TryNormalizeToHectares("2.5 ha");
        Assert.NotNull(haShort);
        Assert.Equal(2.5000m, haShort.Value);
    }

    [Fact]
    public void ExtentNormalizer_ShouldConvertPerchesAndRoods()
    {
        // 160 perches = 1 acre = ~0.4047 ha
        var perches = LandExtentNormalizer.TryNormalizeToHectares("160 perches");
        Assert.NotNull(perches);
        Assert.Equal(0.4047m, perches.Value);

        // 4 roods = 1 acre = ~0.4047 ha
        var roods = LandExtentNormalizer.TryNormalizeToHectares("4 roods");
        Assert.NotNull(roods);
        Assert.Equal(0.4047m, roods.Value);
    }

    [Fact]
    public void ExtentNormalizer_ShouldConvertCompositeNotation()
    {
        // 2A 1R 10P = (2 * 0.40468564) + (1 * 0.10117141) + (10 * 0.00252929) = 0.809371 + 0.101171 + 0.025293 = ~0.9358 ha
        var result = LandExtentNormalizer.TryNormalizeToHectares("2A 1R 10P");
        Assert.NotNull(result);
        Assert.Equal(0.9358m, result.Value);

        var resultComma = LandExtentNormalizer.TryNormalizeToHectares("2A, 1R, 10P");
        Assert.NotNull(resultComma);
        Assert.Equal(0.9358m, resultComma.Value);
    }

    [Fact]
    public void ExtentNormalizer_ShouldConvertSquareMeters()
    {
        var result = LandExtentNormalizer.TryNormalizeToHectares("10000 sqm");
        Assert.NotNull(result);
        Assert.Equal(1.0000m, result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unmeasured")]
    [InlineData("approx 5")]
    [InlineData("-5 ha")]
    [InlineData("five acres")]
    public void ExtentNormalizer_ShouldReturnNull_ForInvalidOrAmbiguousExtent(string? extent)
    {
        var result = LandExtentNormalizer.TryNormalizeToHectares(extent);
        Assert.Null(result);
    }

    // ==========================================
    // 3. Lease Period Parsing Tests
    // ==========================================

    [Theory]
    [InlineData("30 years", 30)]
    [InlineData("30 Years", 30)]
    [InlineData("30", 30)]
    [InlineData("99 years", 99)]
    [InlineData("50 yrs", 50)]
    [InlineData("1 yr", 1)]
    public void LeasePeriodNormalizer_ShouldParseYears_WhenDeterministic(string text, int expected)
    {
        var years = LeasePeriodNormalizer.TryParseYears(text);
        Assert.NotNull(years);
        Assert.Equal(expected, years.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("long term")]
    [InlineData("undetermined")]
    [InlineData("perpetual")]
    [InlineData("indefinite")]
    [InlineData("10 to 20 years")]
    [InlineData("0 years")]
    [InlineData("-5 years")]
    public void LeasePeriodNormalizer_ShouldReturnNull_WhenAmbiguousOrMissing(string? text)
    {
        var years = LeasePeriodNormalizer.TryParseYears(text);
        Assert.Null(years);

        var fallbackYears = LeasePeriodNormalizer.TryParseYears(text, fallback: 0);
        Assert.Equal(0, fallbackYears);
    }

    // ==========================================
    // 4. Investment Normalization Tests
    // ==========================================

    [Theory]
    [InlineData("LKR 50,000,000", 50000000)]
    [InlineData("Rs. 25,000,000", 25000000)]
    [InlineData("Rs. 2500000", 2500000)]
    [InlineData("Sri Lankan Rupees 10,000,000", 10000000)]
    [InlineData("SLR 5,000,000", 5000000)]
    public void InvestmentNormalizer_ShouldPopulateAmount_ForDefinitiveLkr(string text, decimal expected)
    {
        var result = InvestmentNormalizer.NormalizeInvestment(text);
        Assert.NotNull(result.Amount);
        Assert.Equal(expected, result.Amount.Value);
        Assert.True(result.IsLkr);
        Assert.Equal("LKR", result.Currency);
        Assert.Null(result.DiagnosticMessage);
    }

    [Fact]
    public void InvestmentNormalizer_ShouldPopulateAmount_WhenExplicitCurrencyIsLkr()
    {
        var result = InvestmentNormalizer.NormalizeInvestment("50,000,000", "LKR");
        Assert.NotNull(result.Amount);
        Assert.Equal(50000000m, result.Amount.Value);
        Assert.True(result.IsLkr);
        Assert.Null(result.DiagnosticMessage);
    }

    [Theory]
    [InlineData("USD 1,200,000", "USD")]
    [InlineData("EUR 500000", "EUR")]
    [InlineData("$500,000", "$")]
    [InlineData("GBP 750,000", "GBP")]
    public void InvestmentNormalizer_ShouldLeaveAmountUnavailable_ForExplicitNonLkr(string text, string expectedCurrency)
    {
        var result = InvestmentNormalizer.NormalizeInvestment(text);
        Assert.Null(result.Amount);
        Assert.False(result.IsLkr);
        Assert.NotNull(result.Currency);
        Assert.Equal(expectedCurrency, result.Currency);
        Assert.Contains("is not represented by the C4 compliance contract", result.DiagnosticMessage);
    }

    [Fact]
    public void InvestmentNormalizer_ShouldLeaveAmountUnavailable_WhenExplicitCurrencyIsUsd()
    {
        var result = InvestmentNormalizer.NormalizeInvestment("1,200,000", "USD");
        Assert.Null(result.Amount);
        Assert.False(result.IsLkr);
        Assert.Equal("USD", result.Currency);
        Assert.Contains("source currency USD is not represented by the C4 compliance contract", result.DiagnosticMessage);
    }

    [Theory]
    [InlineData("50,000,000")]
    [InlineData("1000000")]
    public void InvestmentNormalizer_ShouldLeaveAmountUnavailable_WhenCurrencyAbsent(string text)
    {
        var result = InvestmentNormalizer.NormalizeInvestment(text);
        Assert.Null(result.Amount);
        Assert.False(result.IsLkr);
        Assert.Null(result.Currency);
        Assert.Contains("currency is absent and cannot be assumed", result.DiagnosticMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tbd")]
    [InlineData("pending loan")]
    [InlineData("-1000")]
    [InlineData("LKR pending")]
    public void InvestmentNormalizer_ShouldReturnNullAmount_WhenInvalid(string? text)
    {
        var result = InvestmentNormalizer.NormalizeInvestment(text);
        Assert.Null(result.Amount);
        Assert.NotNull(result.DiagnosticMessage);
    }

    // ==========================================
    // 5. Land Use Normalization Tests
    // ==========================================

    [Theory]
    [InlineData("commercial", "Commercial")]
    [InlineData("retail", "Commercial")]
    [InlineData("hotel", "Commercial")]
    [InlineData("agricultural", "Agricultural")]
    [InlineData("farming", "Agricultural")]
    [InlineData("paddy", "Agricultural")]
    [InlineData("industrial", "Industrial")]
    [InlineData("factory", "Industrial")]
    [InlineData("manufacturing", "Industrial")]
    [InlineData("industrial warehouse", "Industrial")]
    [InlineData("residential", "Residential")]
    [InlineData("housing", "Residential")]
    [InlineData("conservation", "Conservation")]
    [InlineData("forest conservation", "Conservation")]
    [InlineData("ecotourism", "Ecotourism")]
    [InlineData("eco-tourism", "Ecotourism")]
    [InlineData("eco-tourism resort", "Ecotourism")]
    public void LandUseNormalizer_ShouldNormalizeKnownUses(string input, string expected)
    {
        var normalized = LandUseNormalizer.TryNormalizeLandUse(input);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("commercial agriculture")]
    [InlineData("commercial conservation project")]
    [InlineData("commercial eco-tourism and conservation project")]
    [InlineData("industrial residential")]
    public void LandUseNormalizer_ShouldReturnNull_ForAmbiguousMultiCategoryUses(string input)
    {
        var normalized = LandUseNormalizer.TryNormalizeLandUse(input);
        Assert.Null(normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown text")]
    [InlineData("miscellaneous")]
    [InlineData("random activity")]
    public void LandUseNormalizer_ShouldReturnNull_ForUnmappedUses(string? input)
    {
        var normalized = LandUseNormalizer.TryNormalizeLandUse(input);
        Assert.Null(normalized);
    }

    // ==========================================
    // 6. Compliance Result Translation Tests
    // ==========================================

    [Fact]
    public void ComplianceTranslator_ShouldReturnCleared_WhenCompliantWithNoViolationsOrConditions()
    {
        var result = new ComplianceResultDto(
            Status: "Compliant",
            Violations: Array.Empty<ViolationDto>(),
            Conditions: Array.Empty<ComplianceConditionDto>(),
            Findings: new[]
            {
                new ComplianceFindingDto("R1", "1.0", "Completeness", "Applicable", "Compliant", "Low", false, false, "Pass", "Pass", "Provided", "Calculated", "Auth", "Doc", "Sec", "None")
            },
            DeterministicEvaluationId: "eval-123"
        );

        var (outcome, remarks) = ComplianceResultTranslator.Translate(result);
        Assert.Equal(ScreeningOutcome.Cleared, outcome);
        Assert.Contains("Compliant", remarks);
        Assert.Contains("eval-123", remarks);
    }

    [Fact]
    public void ComplianceTranslator_ShouldReturnAdvisory_WhenConditional()
    {
        var result = new ComplianceResultDto(
            Status: "Conditional",
            Violations: Array.Empty<ViolationDto>(),
            Conditions: new[] { new ComplianceConditionDto("Obtain local permit within 30 days", DateTime.UtcNow.AddDays(30)) },
            Findings: Array.Empty<ComplianceFindingDto>(),
            DeterministicEvaluationId: "eval-456"
        );

        var (outcome, remarks) = ComplianceResultTranslator.Translate(result);
        Assert.Equal(ScreeningOutcome.Advisory, outcome);
        Assert.Contains("Conditional", remarks);
    }

    [Fact]
    public void ComplianceTranslator_ShouldReturnAdvisory_WhenRequiresHumanReviewOrInsufficientInfo()
    {
        var result = new ComplianceResultDto(
            Status: "RequiresHumanReview",
            Violations: Array.Empty<ViolationDto>(),
            Conditions: Array.Empty<ComplianceConditionDto>(),
            Findings: new[]
            {
                new ComplianceFindingDto("R2", "1.0", "Review", "Applicable", "RequiresHumanReview", "Medium", false, true, "Review needed", "Review required", "Missing", "MissingInputs", "Auth", "Doc", "Sec", "Human review")
            }
        );

        var (outcome, _) = ComplianceResultTranslator.Translate(result);
        Assert.Equal(ScreeningOutcome.Advisory, outcome);

        var insufficientResult = new ComplianceResultDto(
            Status: "InsufficientInformation",
            Violations: Array.Empty<ViolationDto>(),
            Conditions: Array.Empty<ComplianceConditionDto>()
        );
        var (insufficientOutcome, _) = ComplianceResultTranslator.Translate(insufficientResult);
        Assert.Equal(ScreeningOutcome.Advisory, insufficientOutcome);
    }

    [Fact]
    public void ComplianceTranslator_ShouldReturnBlocked_WhenNonCompliantOrViolationsExist()
    {
        var nonCompliantResult = new ComplianceResultDto(
            Status: "NonCompliant",
            Violations: new[] { new ViolationDto("RULE_01", "Duration exceeds limit") },
            Conditions: Array.Empty<ComplianceConditionDto>()
        );

        var (outcome, remarks) = ComplianceResultTranslator.Translate(nonCompliantResult);
        Assert.Equal(ScreeningOutcome.Blocked, outcome);
        Assert.Contains("Blocked", remarks);
        Assert.Contains("RULE_01", remarks);
    }

    [Fact]
    public void ComplianceTranslator_ShouldReturnBlocked_WhenBlockingFindingPresent()
    {
        var resultWithBlockingFinding = new ComplianceResultDto(
            Status: "RequiresHumanReview",
            Violations: Array.Empty<ViolationDto>(),
            Conditions: Array.Empty<ComplianceConditionDto>(),
            Findings: new[]
            {
                new ComplianceFindingDto("CRIT_01", "1.0", "Dispute", "Applicable", "NonCompliant", "High", IsBlocking: true, RequiresHumanReview: true, "Dispute", "No dispute", "Missing", "MissingInputs", "Auth", "Doc", "Sec", "Halt")
            }
        );

        var (outcome, _) = ComplianceResultTranslator.Translate(resultWithBlockingFinding);
        Assert.Equal(ScreeningOutcome.Blocked, outcome);
    }

    [Fact]
    public void ComplianceTranslator_ShouldReturnBlocked_ForNullOrUnknownStatus()
    {
        var (nullOutcome, nullRemarks) = ComplianceResultTranslator.Translate(null);
        Assert.Equal(ScreeningOutcome.Blocked, nullOutcome);
        Assert.Contains("Blocked", nullRemarks);

        var unknownResult = new ComplianceResultDto("BizarreStatus", Array.Empty<ViolationDto>(), Array.Empty<ComplianceConditionDto>());
        var (unknownOutcome, _) = ComplianceResultTranslator.Translate(unknownResult);
        Assert.Equal(ScreeningOutcome.Blocked, unknownOutcome);
    }

    // ==========================================
    // 7. Component 4 Screening Command Builder Tests
    // ==========================================

    [Fact]
    public void BuildEvaluateComplianceCommand_ShouldPreserveExactIdsAndCorrelations()
    {
        var requestId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();
        var appRef = "SLG-2026-APP-099";

        var facts = new List<ScreeningFactEntryDto>
        {
            new(Guid.NewGuid(), "district", "Hambantota", "String", "Confirmed", Guid.NewGuid(), DateTime.UtcNow),
            new(Guid.NewGuid(), "divisional_secretariat_division", "Ambalantota", "String", "Confirmed", Guid.NewGuid(), DateTime.UtcNow),
            new(Guid.NewGuid(), "land_location", "Coastal Belt Zone B", "String", "Confirmed", Guid.NewGuid(), DateTime.UtcNow),
            new(Guid.NewGuid(), "land_extent", "10", "String", "Confirmed", Guid.NewGuid(), DateTime.UtcNow),
            new(Guid.NewGuid(), "land_extent_unit", "acres", "String", "Confirmed", Guid.NewGuid(), DateTime.UtcNow),
            new(Guid.NewGuid(), "requested_lease_period", "30 years", "String", "Confirmed", Guid.NewGuid(), DateTime.UtcNow),
            new(Guid.NewGuid(), "investment_amount", "50,000,000", "String", "Confirmed", Guid.NewGuid(), DateTime.UtcNow),
            new(Guid.NewGuid(), "investment_currency", "LKR", "String", "Confirmed", Guid.NewGuid(), DateTime.UtcNow),
            new(Guid.NewGuid(), "proposed_land_use", "ecotourism", "String", "Confirmed", Guid.NewGuid(), DateTime.UtcNow),
            new(Guid.NewGuid(), "legal_statutory_basis", "State Lands Ordinance", "String", "Confirmed", Guid.NewGuid(), DateTime.UtcNow),
            new(Guid.NewGuid(), "project_purpose", "Eco Resort", "String", "Confirmed", Guid.NewGuid(), DateTime.UtcNow),
            new(Guid.NewGuid(), "project_description", "Sustainable resort with solar power", "String", "Confirmed", Guid.NewGuid(), DateTime.UtcNow)
        };

        var request = new Component4ScreeningRequest(
            ScreeningRequestId: requestId,
            LeaseCaseId: caseId,
            ApplicationReference: appRef,
            VerifiedFactSnapshotId: snapshotId,
            Facts: facts,
            RequestedAtUtc: DateTime.UtcNow
        );

        var command = Component4ScreeningGateway.BuildEvaluateComplianceCommand(request);

        Assert.NotNull(command);
        Assert.Equal(30, command.LeaseDurationYears);
        Assert.Equal("Ecotourism", command.ProposedUse);
        Assert.Equal(50000000m, command.LeaseAmount);

        var input = command.Input;
        Assert.NotNull(input);
        Assert.Equal(appRef, input.ProposalId);

        // Location check
        Assert.NotNull(input.Location);
        Assert.Equal("Southern", input.Location.Province); // Deterministically mapped from Hambantota!
        Assert.Equal("Hambantota", input.Location.District);
        Assert.Equal("Ambalantota", input.Location.DivisionalSecretariatDivision);
        Assert.Null(input.Location.GramaNiladhariDivision); // GND remains missing (not extracted)
        Assert.Equal("Coastal Belt Zone B", input.Location.Description);

        // Land Requirement check
        Assert.NotNull(input.LandRequirement);
        Assert.True(input.LandRequirement.RequiresLand);
        Assert.Equal(4.0469m, input.LandRequirement.ExtentHectares); // 10 acres normalized
        Assert.Equal("Ecotourism", input.LandRequirement.LandType);
        Assert.Contains("Eco Resort", input.LandRequirement.AllocationDetails);
        Assert.Contains("Sustainable resort", input.LandRequirement.AllocationDetails);

        // Budget check
        Assert.NotNull(input.Budget);
        Assert.Equal(50000000m, input.Budget.SubmittedProjectBudget);

        // Policy alignment check (legal basis preserved without inventing section codes)
        Assert.NotNull(input.PolicyAlignment);
        Assert.Contains("State Lands Ordinance", input.PolicyAlignment!.DeclaredPolicies!);
    }

    [Fact]
    public void BuildEvaluateComplianceCommand_ShouldHandleMissingAndUnknownFieldsWithoutGuessing()
    {
        var requestId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();

        var facts = new List<ScreeningFactEntryDto>
        {
            new(Guid.NewGuid(), "district", "UnknownOrUnspecified", "String", "Confirmed", Guid.NewGuid(), DateTime.UtcNow),
            new(Guid.NewGuid(), "proposed_land_use", "Space Tourism", "String", "Confirmed", Guid.NewGuid(), DateTime.UtcNow)
        };

        var request = new Component4ScreeningRequest(
            ScreeningRequestId: requestId,
            LeaseCaseId: caseId,
            ApplicationReference: "APP-MINIMAL",
            VerifiedFactSnapshotId: snapshotId,
            Facts: facts,
            RequestedAtUtc: DateTime.UtcNow
        );

        var command = Component4ScreeningGateway.BuildEvaluateComplianceCommand(request);

        Assert.NotNull(command.Input);
        Assert.Null(command.Input.Location?.Province); // Unknown district leaves Province null
        Assert.Null(command.Input.Location?.GramaNiladhariDivision); // GND null
        Assert.Null(command.Input.LandRequirement?.ExtentHectares); // Missing extent is null
        Assert.Null(command.Input.LandRequirement?.LandType); // Unrecognized use remains null in C4 taxonomy
        Assert.Null(command.Input.Budget); // Missing budget remains null
        Assert.Null(command.Input.PolicyAlignment); // Missing legal basis remains null
    }
}
