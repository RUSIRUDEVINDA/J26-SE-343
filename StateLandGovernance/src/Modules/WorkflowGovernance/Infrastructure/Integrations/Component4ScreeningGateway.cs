namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.Screening;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Exceptions;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Mappers;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Real in-process Anti-Corruption Layer (ACL) gateway integrating Component 3 (WorkflowGovernance)
/// with Component 4 (GovernanceIntelligence) pre-workflow regulatory compliance screening.
/// 
/// Key Guarantees:
/// 1. Consumes only authoritative human-verified facts from VerifiedFactSnapshot (Component4ScreeningRequest).
/// 2. Treats Component 4 as strictly read-only; adapts C3 flat verified facts to C4 EvaluateComplianceCommand DTOs.
/// 3. Normalizes District -> Province, extent units -> Hectares, lease duration -> Years, investment -> Decimal, and land-use taxonomy.
/// 4. Preserves correlation context (ScreeningRequestId, LeaseCaseId, VerifiedFactSnapshotId).
/// 5. Exhaustively translates C4 ComplianceResultDto to C3 ScreeningOutcome (Cleared, Advisory, Blocked).
/// 6. Safe failure mode: Technical evaluation faults never mutate screening to Cleared and leave the case Pending/Blocked.
/// </summary>
public sealed class Component4ScreeningGateway : IComponent4ScreeningGateway
{
    private readonly EvaluateComplianceCommandHandler? _complianceHandler;
    private readonly IServiceProvider? _serviceProvider;
    private readonly ILeaseCaseRepository _leaseCaseRepository;
    private readonly ICommandHandler<RecordScreeningResultCommand, ScreeningResultDto> _recordScreeningHandler;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<Component4ScreeningGateway>? _logger;

    public Component4ScreeningGateway(
        IServiceProvider serviceProvider,
        ILeaseCaseRepository leaseCaseRepository,
        ICommandHandler<RecordScreeningResultCommand, ScreeningResultDto> recordScreeningHandler,
        TimeProvider? timeProvider = null,
        ILogger<Component4ScreeningGateway>? logger = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _leaseCaseRepository = leaseCaseRepository ?? throw new ArgumentNullException(nameof(leaseCaseRepository));
        _recordScreeningHandler = recordScreeningHandler ?? throw new ArgumentNullException(nameof(recordScreeningHandler));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger;
    }

    public Component4ScreeningGateway(
        EvaluateComplianceCommandHandler complianceHandler,
        ILeaseCaseRepository leaseCaseRepository,
        ICommandHandler<RecordScreeningResultCommand, ScreeningResultDto> recordScreeningHandler,
        TimeProvider? timeProvider = null,
        ILogger<Component4ScreeningGateway>? logger = null)
    {
        _complianceHandler = complianceHandler ?? throw new ArgumentNullException(nameof(complianceHandler));
        _leaseCaseRepository = leaseCaseRepository ?? throw new ArgumentNullException(nameof(leaseCaseRepository));
        _recordScreeningHandler = recordScreeningHandler ?? throw new ArgumentNullException(nameof(recordScreeningHandler));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger;
    }

    /// <summary>
    /// Dispatches a verified-fact screening request to Component 4, translates the evaluation result,
    /// and records the outcome back onto the LeaseCase aggregate.
    /// </summary>
    public async Task DispatchScreeningRequestAsync(
        Component4ScreeningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Build C4 command via ACL anti-corruption mapper
        var c4Command = BuildEvaluateComplianceCommand(request);

        // 2. Resolve C4 handler
        var handler = _complianceHandler
            ?? _serviceProvider?.GetService<EvaluateComplianceCommandHandler>()
            ?? throw new InvalidOperationException("EvaluateComplianceCommandHandler is not registered in the service container. Ensure Component 4 services are registered.");

        // 3. Invoke existing C4 application handler in-process
        ComplianceResultDto c4Result;
        try
        {
            c4Result = await handler.HandleAsync(c4Command, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Component 4 compliance evaluation failed for LeaseCase {LeaseCaseId}, Request {ScreeningRequestId}.",
                request.LeaseCaseId, request.ScreeningRequestId);

            // Technical failure must never record Cleared. Case remains Pending, blocking the workflow gate.
            throw new Component4ScreeningException(
                request.ScreeningRequestId,
                request.LeaseCaseId,
                $"Component 4 compliance evaluation failed: {ex.Message}",
                ex);
        }

        // 3. Translate C4 compliance result into Component 3 ScreeningOutcome and Remarks
        var (outcome, remarks) = ComplianceResultTranslator.Translate(c4Result);

        // 4. Retrieve current LeaseCase to obtain optimistic concurrency revision
        var leaseCase = await _leaseCaseRepository.GetByIdAsync(
            new LeaseCaseId(request.LeaseCaseId),
            cancellationToken);

        if (leaseCase == null)
        {
            throw new LeaseCaseNotFoundException(request.LeaseCaseId);
        }

        // 5. Record verified screening result back to LeaseCase aggregate preserving snapshot binding
        var assessedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var recordCommand = new RecordScreeningResultCommand(
            LeaseCaseId: request.LeaseCaseId,
            ScreeningRequestId: request.ScreeningRequestId,
            VerifiedFactSnapshotId: request.VerifiedFactSnapshotId,
            Outcome: outcome.ToString(),
            ExpectedLeaseCaseRevision: leaseCase.Revision,
            ScreeningResultId: request.ScreeningRequestId,
            Remarks: remarks,
            AssessedAtUtc: assessedAt
        );

        await _recordScreeningHandler.HandleAsync(recordCommand, cancellationToken);
    }

    /// <summary>
    /// Public adapter mapping Component 3 verified facts to Component 4 EvaluateComplianceCommand.
    /// </summary>
    public static EvaluateComplianceCommand BuildEvaluateComplianceCommand(Component4ScreeningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Index verified facts by case-insensitive code
        var facts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (request.Facts != null)
        {
            foreach (var fact in request.Facts)
            {
                if (!string.IsNullOrWhiteSpace(fact.FactCode))
                {
                    facts[fact.FactCode.Trim()] = fact.CanonicalValue ?? string.Empty;
                }
            }
        }

        string? GetFact(string code) => facts.TryGetValue(code, out var val) ? val : null;

        // Fact extractions
        var district = GetFact("district");
        var dsd = GetFact("divisional_secretariat_division");
        var landLocation = GetFact("land_location");
        var landExtent = GetFact("land_extent");
        var landExtentUnit = GetFact("land_extent_unit");
        var leasePeriod = GetFact("requested_lease_period");
        var investment = GetFact("investment_amount");
        var investmentCurrency = GetFact("investment_currency");
        var proposedUse = GetFact("proposed_land_use");
        var legalBasis = GetFact("legal_statutory_basis");
        var projectPurpose = GetFact("project_purpose");
        var projectDescription = GetFact("project_description");

        // Normalizations
        var province = DistrictToProvinceMapper.TryMapDistrictToProvince(district);
        var extentHectares = LandExtentNormalizer.TryNormalizeToHectares(landExtent, landExtentUnit);
        var leaseYears = LeasePeriodNormalizer.TryParseYears(leasePeriod);
        var investmentResult = InvestmentNormalizer.NormalizeInvestment(investment, investmentCurrency);
        var budgetAmount = investmentResult.Amount;
        var normalizedLandUse = LandUseNormalizer.TryNormalizeLandUse(proposedUse);

        // Combine purpose, description, and explicit lease duration (if available) for AllocationDetails
        var allocationParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(projectPurpose)) allocationParts.Add(projectPurpose.Trim());
        if (!string.IsNullOrWhiteSpace(projectDescription)) allocationParts.Add(projectDescription.Trim());
        if (leaseYears.HasValue) allocationParts.Add($"Lease Term: {leaseYears.Value} years");
        var allocationDetails = allocationParts.Count > 0 ? string.Join(" - ", allocationParts) : null;

        // Location DTO (GND is not extracted from proposal text; passed as null)
        var locationDto = new ProjectLocationDto(
            Province: province,
            District: !string.IsNullOrWhiteSpace(district) ? district.Trim() : null,
            DivisionalSecretariatDivision: !string.IsNullOrWhiteSpace(dsd) ? dsd.Trim() : null,
            GramaNiladhariDivision: null,
            Description: !string.IsNullOrWhiteSpace(landLocation) ? landLocation.Trim() : null
        );

        // Land Requirement DTO
        var landRequirementDto = new LandRequirementDto(
            RequiresLand: true,
            ExtentHectares: extentHectares,
            LandType: normalizedLandUse,
            AllocationDetails: allocationDetails,
            ResettlementApplicable: null
        );

        // Budget DTO
        BudgetDto? budgetDto = null;
        if (budgetAmount.HasValue)
        {
            budgetDto = new BudgetDto(
                SubmittedProjectBudget: budgetAmount.Value,
                CostComponents: null
            );
        }

        // Policy Alignment DTO
        PolicyAlignmentDto? policyAlignmentDto = null;
        if (!string.IsNullOrWhiteSpace(legalBasis))
        {
            policyAlignmentDto = new PolicyAlignmentDto(
                DeclaredPolicies: new[] { legalBasis.Trim() },
                AlignmentExplanation: "Declared legal/statutory basis from verified proposal facts."
            );
        }

        var proposalId = !string.IsNullOrWhiteSpace(request.ApplicationReference)
            ? request.ApplicationReference
            : request.LeaseCaseId.ToString();

        var inputDto = new ProposalComplianceInputDto(
            ProposalId: proposalId,
            Location: locationDto,
            LandRequirement: landRequirementDto,
            PreliminaryAssessment: null,
            PolicyAlignment: policyAlignmentDto,
            Stakeholders: null,
            ResultFramework: null,
            ImpactAssessment: null,
            RiskFramework: null,
            DisasterRiskAssessment: null,
            MonitoringPlan: null,
            Budget: budgetDto,
            Financing: null,
            SocialSafeguard: null,
            Implementation: null,
            EconomicAppraisal: null,
            EvidenceReferences: null
        );

        return new EvaluateComplianceCommand(
            ActionName: $"ProposalScreening_{proposalId}",
            LeaseDurationYears: leaseYears ?? 0,
            ProposedUse: normalizedLandUse ?? proposedUse,
            LeaseAmount: budgetAmount ?? 0m,
            ZoningArea: null,
            Input: inputDto
        );
    }
}
