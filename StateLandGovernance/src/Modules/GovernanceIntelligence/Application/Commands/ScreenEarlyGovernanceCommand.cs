using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Application.Commands;

/// <summary>
/// Application command to evaluate recorded case concerns for early governance screening.
/// <para>
/// Boundary notes:
/// - Verified evidence states are caller assertions.
/// - This command performs evaluation only; authorization, authentication of evidence,
///   and persistence are managed outside this handler.
/// </para>
/// </summary>
public sealed record ScreenEarlyGovernanceCommand(
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

    public ScreenEarlyGovernanceCommandHandler(IEarlyGovernanceScreeningEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public Task<EarlyGovernanceScreeningResultDto> HandleAsync(
        ScreenEarlyGovernanceCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command is null)
        {
            throw new ArgumentNullException(nameof(command), "Command cannot be null.");
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

        return Task.FromResult(resultDto);
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
