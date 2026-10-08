using System.Collections.ObjectModel;
using System.Text;
using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

namespace StateLandGovernance.GovernanceIntelligence.Application.Commands;

/// <summary>
/// Requests an advisory classification for a complaint associated with an existing case reference.
/// The case reference is stored as supplied; this component does not own or validate the referenced case.
/// </summary>
public sealed record ClassifyComplaintCommand(string CaseId, string ComplaintText);

/// <summary>
/// Orchestrates one classifier call followed by persistence of a new assessment snapshot.
/// Repeating the command creates another assessment because no caller idempotency key exists.
/// </summary>
public sealed class ClassifyComplaintCommandHandler
{
    private readonly IComplaintClassificationClient _client;
    private readonly IComplaintClassificationAssessmentStore _store;
    private readonly TimeProvider _timeProvider;

    public ClassifyComplaintCommandHandler(
        IComplaintClassificationClient client,
        IComplaintClassificationAssessmentStore store,
        TimeProvider timeProvider)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ComplaintClassificationAssessmentResult> HandleAsync(
        ClassifyComplaintCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(command);

        var validationError = Validate(command);
        if (validationError is not null)
        {
            return validationError;
        }

        var classification = await _client.ClassifyAsync(
            command.ComplaintText,
            command.CaseId,
            cancellationToken).ConfigureAwait(false);

        if (!classification.IsSuccess)
        {
            return ComplaintClassificationAssessmentResult.Failure(
                classification.Error ?? new ComplaintClassificationError(
                    ComplaintClassificationErrorCode.InvalidResponse,
                    "The complaint classifier returned neither a prediction nor an error."));
        }

        var prediction = classification.Prediction!;
        if (!string.Equals(prediction.CaseId, command.CaseId, StringComparison.Ordinal))
        {
            return ComplaintClassificationAssessmentResult.Failure(
                ComplaintClassificationErrorCode.InvalidResponse,
                "The complaint classifier response did not preserve the supplied case reference.");
        }

        var probabilities = new ReadOnlyDictionary<string, double>(
            new Dictionary<string, double>(prediction.ClassProbabilities, StringComparer.Ordinal));
        var assessment = new ComplaintClassificationAssessment(
            Guid.NewGuid(),
            command.CaseId,
            command.ComplaintText,
            prediction.ModelVersion,
            prediction.PredictedCategory,
            probabilities,
            prediction.AdvisoryNote,
            prediction.ClosedSetNote,
            _timeProvider.GetUtcNow());

        await _store.AddAsync(assessment, cancellationToken).ConfigureAwait(false);

        return ComplaintClassificationAssessmentResult.Success(assessment);
    }

    private static ComplaintClassificationAssessmentResult? Validate(ClassifyComplaintCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.CaseId))
        {
            return ComplaintClassificationAssessmentResult.Failure(
                ComplaintClassificationErrorCode.InvalidInput,
                "CaseId is required.");
        }

        if (command.CaseId.Length > ComplaintClassificationConstraints.MaximumCaseIdLength)
        {
            return ComplaintClassificationAssessmentResult.Failure(
                ComplaintClassificationErrorCode.InvalidInput,
                $"CaseId must not exceed {ComplaintClassificationConstraints.MaximumCaseIdLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(command.ComplaintText))
        {
            return ComplaintClassificationAssessmentResult.Failure(
                ComplaintClassificationErrorCode.InvalidInput,
                "ComplaintText is required.");
        }

        if (command.ComplaintText.EnumerateRunes().Count() > ComplaintClassificationConstraints.MaximumComplaintTextLength)
        {
            return ComplaintClassificationAssessmentResult.Failure(
                ComplaintClassificationErrorCode.InvalidInput,
                $"ComplaintText must not exceed {ComplaintClassificationConstraints.MaximumComplaintTextLength} Unicode characters.");
        }

        return null;
    }
}
