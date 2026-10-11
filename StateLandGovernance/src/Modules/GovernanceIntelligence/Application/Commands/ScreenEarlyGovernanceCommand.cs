using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Application.Commands;

/// <summary>
/// Application command to evaluate recorded case concerns for early governance screening.
/// <para>
/// Boundary notes:
/// - Verified evidence states are caller assertions.
/// - Authorization and authentication of evidence are managed outside this handler.
/// - AssessmentId and WorkflowRunId must be supplied by the caller; neither is inferred.
/// </para>
/// </summary>
public sealed record ScreenEarlyGovernanceCommand(
    Guid AssessmentId,
    Guid WorkflowRunId,
    string CaseId,
    string InputVersion,
    IReadOnlyList<EarlyGovernanceIndicatorDto>? Indicators
);

/// <summary>
/// Command handler for evaluating early governance screening requests against the domain screening engine.
/// </summary>
public sealed class ScreenEarlyGovernanceCommandHandler
{
    private readonly IEarlyGovernanceScreeningEngine _engine;
    private readonly IEarlyGovernanceScreeningStore _store;
    private readonly IEarlyGovernanceReferralPolicy _referralPolicy;
    private readonly TimeProvider _timeProvider;

    public ScreenEarlyGovernanceCommandHandler(
        IEarlyGovernanceScreeningEngine engine,
        IEarlyGovernanceScreeningStore store,
        IEarlyGovernanceReferralPolicy referralPolicy,
        TimeProvider timeProvider)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _referralPolicy = referralPolicy ?? throw new ArgumentNullException(nameof(referralPolicy));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<EarlyGovernanceScreeningResultDto> HandleAsync(
        ScreenEarlyGovernanceCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command is null)
        {
            throw new ArgumentNullException(nameof(command), "Command cannot be null.");
        }

        if (command.AssessmentId == Guid.Empty)
        {
            throw new ArgumentException("AssessmentId cannot be empty.", nameof(command));
        }

        if (command.WorkflowRunId == Guid.Empty)
        {
            throw new ArgumentException("WorkflowRunId cannot be empty.", nameof(command));
        }

        if (command.Indicators is null)
        {
            throw new ArgumentException("Indicators collection cannot be null.", nameof(command));
        }

        // Validate individual collection elements
        for (int i = 0; i < command.Indicators.Count; i++)
        {
            if (command.Indicators[i] is null)
            {
                throw new ArgumentException($"Indicator at index {i} cannot be null.", nameof(command));
            }
        }

        // Strict symbolic enum parsing & domain record construction
        var domainRecords = new List<EarlyGovernanceIndicatorRecord>(command.Indicators.Count);
        for (int i = 0; i < command.Indicators.Count; i++)
        {
            var dto = command.Indicators[i];
            var indicatorType = ParseSymbolicEnum<EarlyGovernanceIndicatorType>(dto.IndicatorType, nameof(dto.IndicatorType), i);
            var evidenceState = ParseSymbolicEnum<EarlyGovernanceEvidenceState>(dto.EvidenceState, nameof(dto.EvidenceState), i);

            domainRecords.Add(new EarlyGovernanceIndicatorRecord(
                indicatorType,
                evidenceState,
                dto.EvidenceReference,
                dto.RecordedAtUtc));
        }

        // Construct domain input snapshot (delegates domain validations: CaseId, InputVersion, duplicates, provenance)
        var domainInput = new EarlyGovernanceScreeningInput(command.CaseId, command.InputVersion, domainRecords);

        cancellationToken.ThrowIfCancellationRequested();

        // Evaluate screening through the domain engine
        var domainResult = _engine.Screen(domainInput);

        // Map domain result to application DTOs preserving ordering and canonical enum names
        var indicatorResultDtos = domainResult.IndicatorResults
            .Select(r => new EarlyGovernanceIndicatorResultDto(
                r.IndicatorType.ToString(),
                r.EvidenceState.ToString(),
                r.RequiresReview,
                r.NeedsEvidence,
                r.ReasonCode,
                r.Message,
                r.EvidenceReference,
                r.RecordedAtUtc))
            .ToList();

        var resultDto = new EarlyGovernanceScreeningResultDto(
            domainResult.CaseId,
            domainResult.InputVersion,
            domainResult.OverallStatus.ToString(),
            domainResult.HasIncompleteEvidence,
            indicatorResultDtos);

        var assessedAtUtc = _timeProvider.GetUtcNow();
        var referralDecision = _referralPolicy.Evaluate(resultDto);
        EarlyGovernanceReferralIntentDto? referral = null;
        if (referralDecision.Status == EarlyGovernanceReferralMappingStatus.ReferralRequired)
        {
            if (string.IsNullOrWhiteSpace(referralDecision.DecisionCode) ||
                string.IsNullOrWhiteSpace(referralDecision.Reason))
            {
                throw new InvalidOperationException("A referral policy decision must include a decision code and reason.");
            }

            var evidenceReferences = (referralDecision.EvidenceReferences ?? Array.Empty<string>())
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            referral = new EarlyGovernanceReferralIntentDto(
                Guid.NewGuid(),
                command.AssessmentId,
                command.AssessmentId,
                resultDto.CaseId,
                command.WorkflowRunId,
                referralDecision.DecisionCode,
                referralDecision.Reason,
                evidenceReferences,
                assessedAtUtc);
        }

        await _store.AddAssessmentAsync(
            command.AssessmentId,
            command.WorkflowRunId,
            assessedAtUtc,
            resultDto,
            referral,
            cancellationToken).ConfigureAwait(false);

        return resultDto;
    }

    /// <summary>
    /// Strictly parses a symbolic enum name case-insensitively, rejecting numeric strings, signed numbers,
    /// comma-separated lists, unknown names, null, or whitespace.
    /// </summary>
    private static TEnum ParseSymbolicEnum<TEnum>(string? rawValue, string fieldName, int index)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new ArgumentException($"Indicator at index {index} has null or whitespace {fieldName}.", fieldName);
        }

        string trimmed = rawValue.Trim();

        // Match against defined symbolic names only
        string[] definedNames = Enum.GetNames<TEnum>();
        string? matchedName = definedNames.FirstOrDefault(name => string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase));

        if (matchedName is null)
        {
            throw new ArgumentException($"Indicator at index {index} has invalid {fieldName} '{trimmed}'. Must be a defined symbolic name.", fieldName);
        }

        return Enum.Parse<TEnum>(matchedName);
    }
}
