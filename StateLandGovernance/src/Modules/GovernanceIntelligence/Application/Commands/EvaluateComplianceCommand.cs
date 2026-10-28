using System.Text.Json;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
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
/// Command to evaluate regulatory compliance for a proposed lease or NPD project proposal.
/// </summary>
public sealed record EvaluateComplianceCommand(
    string ActionName,
    int LeaseDurationYears = 0,
    string? ProposedUse = null,
    decimal LeaseAmount = 0m,
    string? ZoningArea = null,
    ProposalComplianceInputDto? Input = null,
    string? CaseId = null,
    Guid? SubmissionKey = null,
    string? InputVersion = null,
    string? SchemaVersion = null,
    string? SourceReference = null,
    RegulatoryCaseBinding? C3Binding = null
){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Guid? EarlyRequestId {get;init;}
}

/// <summary>
/// Handler for the EvaluateComplianceCommand.
/// </summary>
public sealed class EvaluateComplianceCommandHandler
{
    private readonly IRegulatoryRuleProvider _ruleProvider;
    private readonly IRegulatoryComplianceEngine _complianceEngine;
    private readonly IGovernanceEvaluationStore _evaluationStore;
    private readonly IComplianceRuleCatalogue? _ruleCatalogue;
    private readonly TimeProvider _timeProvider;

    public EvaluateComplianceCommandHandler(
        IRegulatoryRuleProvider ruleProvider,
        IRegulatoryComplianceEngine complianceEngine,
        IGovernanceEvaluationStore evaluationStore,
        IComplianceRuleCatalogue? ruleCatalogue = null,
        TimeProvider? timeProvider = null)
    {
        _ruleProvider = ruleProvider ?? throw new ArgumentNullException(nameof(ruleProvider));
        _complianceEngine = complianceEngine ?? throw new ArgumentNullException(nameof(complianceEngine));
        _evaluationStore = evaluationStore ?? throw new ArgumentNullException(nameof(evaluationStore));
        _ruleCatalogue = ruleCatalogue;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ComplianceResultDto> HandleAsync(EvaluateComplianceCommand command, CancellationToken cancellationToken = default, SavedRegulatoryProvenance? provenance = null)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));

        DateTime utcTimestamp = _timeProvider.GetUtcNow().UtcDateTime;
        ComplianceResult result;

        string? proposalId = string.IsNullOrWhiteSpace(command.Input?.ProposalId) ? null : command.Input.ProposalId;
        string? caseId = string.IsNullOrWhiteSpace(command.CaseId) ? null : command.CaseId.Trim();
        if (caseId?.Length > 100)
        {
            throw new ArgumentException("CaseId cannot exceed 100 characters.", nameof(command));
        }
        string catalogueSource = "InMemoryDefault";
        Guid? storedId=null;

        if (command.Input != null)
        {
            var domainInput = ProposalComplianceInputMapper.Map(command.Input);
            if(provenance!=null)provenance=provenance with{EvaluatedInputHash=RegulatoryJson.Hash(JsonSerializer.Serialize(domainInput,RegulatoryJson.Options))};
            if (_ruleCatalogue != null)
            {
                try
                {
                    var activeRules = await _ruleCatalogue.GetActiveRulesAsync(utcTimestamp, cancellationToken);
                    if(provenance!=null&&activeRules.Count==0)throw new RegulatorySubmissionUnavailableException();
                    if(provenance!=null){
                        var ordered=activeRules.OrderBy(x=>x.RuleCode,StringComparer.Ordinal).ThenBy(x=>x.RuleVersion,StringComparer.Ordinal).Select(x=>x with{Parameters=x.Parameters.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal)}).ToArray();
                        provenance=provenance with{CatalogueDigest=RegulatoryJson.Hash(JsonSerializer.Serialize(ordered,RegulatoryJson.Options)),CatalogueVersion=string.Join(";",ordered.Select(x=>x.RuleVersion).Distinct().OrderBy(x=>x,StringComparer.Ordinal))};
                    }
                    catalogueSource = _ruleCatalogue.GetType().Name.Contains("Postgres", StringComparison.OrdinalIgnoreCase)
                        ? "PostgreSQL"
                        : "InMemoryTest";
                    result = _complianceEngine.EvaluateNpd(domainInput, activeRules, utcTimestamp);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    if(provenance!=null)throw new RegulatorySubmissionUnavailableException();
                    catalogueSource = "PostgreSQL (Unavailable/Misconfigured)";
                    var sysFinding = new ComplianceFinding(
                        "SYS-CATALOGUE-001",
                        "1.0",
                        RuleEvaluationType.Completeness,
                        RuleApplicability.Applicable,
                        RuleResultStatus.RequiresHumanReview,
                        "High",
                        IsBlocking: true,
                        RequiresHumanReview: true,
                        ObservedValueSummary: $"Regulatory rule catalogue is unavailable or misconfigured: {ex.Message}",
                        ExpectedRequirement: "PostgreSQL regulatory compliance rule catalogue must be available and valid.",
                        EvidenceStatus: EvidenceStatus.Missing,
                        CalculationStatus: CalculationStatus.MissingInputs,
                        new RuleSourceMetadata(RuleSourceType.ResearchConfiguration, "System Infrastructure", "PostgresComplianceRuleCatalogue", "Configuration"),
                        "Inspect PostgreSQL governance_intelligence.compliance_rules table configuration."
                    );

                    var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"SYS-CATALOGUE|{proposalId}|{ex.Message}"));
                    var deterministicId = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    result = new ComplianceResult(ComplianceStatus.RequiresReview, new[] { sysFinding }, deterministicId, utcTimestamp);
                }
            }
            else
            {
                if(provenance!=null)throw new RegulatorySubmissionUnavailableException();
                result = _complianceEngine.EvaluateNpd(domainInput, utcTimestamp);
            }
        }
        else
        {
            // Legacy evaluation fallback
            var rules = await _ruleProvider.GetActiveRulesAsync(cancellationToken);
            var legacyInput = new LeaseEvaluationInput(
                command.LeaseDurationYears,
                command.ProposedUse ?? string.Empty,
                command.LeaseAmount,
                command.ZoningArea ?? string.Empty);

            result = _complianceEngine.Evaluate(legacyInput, rules, utcTimestamp);
        }

        // Store audit record & evaluation output atomically
        var auditRecord = GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance,
            command.ActionName ?? "EvaluateCompliance",
            result.Status.ToString(),
            $"Findings: {result.Findings.Count}, Violations: {result.Violations.Count}, Conditions: {result.Conditions.Count}, CatalogueSource={catalogueSource}",
            utcTimestamp);

        if(provenance==null){await _evaluationStore.StoreComplianceEvaluationAsync(auditRecord, result, command.ActionName ?? "EvaluateCompliance", proposalId, caseId, cancellationToken);}
        else storedId=await _evaluationStore.StoreOwnedComplianceEvaluationAsync(auditRecord,result,command.ActionName ?? "EvaluateCompliance",proposalId,command.C3Binding?.CaseId.ToString(),provenance,cancellationToken);

        // Map to result DTO
        var violationDtos = result.Violations
            .Select(v => new ViolationDto(v.RuleCode, v.Message))
            .ToList();

        var conditionDtos = result.Conditions
            .Select(c => new ComplianceConditionDto(c.Description, c.RequiredByDate))
            .ToList();

        var findingDtos = result.Findings
            .Select(f => new ComplianceFindingDto(
                f.RuleCode,
                f.RuleVersion,
                f.Category.ToString(),
                f.Applicability.ToString(),
                f.Status.ToString(),
                f.Severity,
                f.IsBlocking,
                f.RequiresHumanReview,
                f.ObservedValueSummary,
                f.ExpectedRequirement,
                f.EvidenceStatus.ToString(),
                f.CalculationStatus.ToString(),
                f.SourceReference?.SourceAuthority ?? string.Empty,
                f.SourceReference?.SourceDocument ?? string.Empty,
                f.SourceReference?.SourceSection ?? string.Empty,
                f.RecommendedAction
            ))
            .ToList();

        return new ComplianceResultDto(
            result.Status.ToString(),
            violationDtos,
            conditionDtos,
            findingDtos,
            result.DeterministicEvaluationId, storedId
        );
    }

    private static ComplianceStatus CombineStatus(ComplianceStatus legacyStatus, ComplianceStatus npdStatus)
    {
        if (legacyStatus == ComplianceStatus.NonCompliant || npdStatus == ComplianceStatus.NonCompliant)
            return ComplianceStatus.NonCompliant;
        if (npdStatus == ComplianceStatus.InsufficientInformation)
            return ComplianceStatus.InsufficientInformation;
        if (legacyStatus == ComplianceStatus.Conditional || npdStatus == ComplianceStatus.Conditional)
            return ComplianceStatus.Conditional;
        if (npdStatus == ComplianceStatus.RequiresHumanReview)
            return ComplianceStatus.RequiresHumanReview;
        return ComplianceStatus.Compliant;
    }

    private static ProposalComplianceInput MapLegacyToProposalInput(EvaluateComplianceCommand cmd)
    {
        return new ProposalComplianceInput(
            ProposalId: string.Empty,
            Location: new ProjectLocationInput("Western", "Colombo", "Colombo DSD", "GND-101", cmd.ZoningArea ?? "Zoning Area"),
            LandRequirement: new LandRequirementInput(true, 1.0m, cmd.ProposedUse ?? "Commercial", $"Legacy Lease {cmd.LeaseDurationYears} Years", false),
            PreliminaryAssessment: new PreliminaryAssessmentInput(true, "PRE-FEAS-LEGACY", true, "EIA-LEGACY", new[] { "DOC-LEGACY-01" }),
            PolicyAlignment: new PolicyAlignmentInput(new[] { "National Land Policy" }, "Legacy Zoning Match"),
            Stakeholders: new StakeholderInput(new[] { "LRA", "DS" }, new[] { "LRA", "DS" }, true, "N/A"),
            ResultFramework: new ResultFrameworkInput(new[] { new ResultFrameworkNodeInput("N-1", "Impact", "Development", "", new[] { "KPI-1" }) }),
            ImpactAssessment: new ImpactAssessmentInput(false, Array.Empty<NegativeImpactItemInput>()),
            RiskFramework: new RiskFrameworkInput(new[] { new RiskItemInput("R-1", "Operational Risk", "Mitigated", false) }),
            MonitoringPlan: new MonitoringPlanInput(new[] { new KpiInput("KPI-1", "N-1", "Units", 0, 2024, 100, "Inspection", "Registry", "Officer") }),
            Budget: new BudgetInput(cmd.LeaseAmount, new[] { new CostComponentInput("C-1", "Lease Fee", cmd.LeaseAmount) }),
            Financing: new FinancingInput(new[] { new FinancingSourceInput("F-1", "Equity", cmd.LeaseAmount) }, false, null),
            SocialSafeguard: new SocialSafeguardInput(false, null, true, true),
            Implementation: new ImplementationInput(new[] { new ImplementationActivityInput("A-1", "Lease Execution", "Land Officer") }, "Standard", 0m, "Internal"),
            EconomicAppraisal: new EconomicAppraisalInput("NPV", 0.10m, new[] { -cmd.LeaseAmount, cmd.LeaseAmount * 0.2m, cmd.LeaseAmount * 0.3m }, CashFlowPeriod.Annual, null, null, null, null, null, null, CostBenefitRatioConvention.BenefitsOverCosts),
            EvidenceReferences: new[] { new EvidenceReferenceInput("EV-1", "REF-001", EvidenceStatus.Verified) }
        );
    }

}
