using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence.ComplaintClassification;

public sealed class ClassifyComplaintCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtc =
        new(2026, 10, 8, 6, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_ValidCommand_ClassifiesAndPersistsCompleteAssessment()
    {
        const string caseId = "CASE-C4-001";
        const string complaintText = "  The allocation record was altered without approval.  ";
        using var cancellationSource = new CancellationTokenSource();
        var client = new RecordingClient(_ => SuccessfulClassification(caseId));
        var store = new RecordingStore();
        var handler = CreateHandler(client, store);

        var result = await handler.HandleAsync(
            new ClassifyComplaintCommand(caseId, complaintText),
            cancellationSource.Token);

        Assert.True(result.IsSuccess);
        var assessment = Assert.IsType<ComplaintClassificationAssessment>(result.Assessment);
        Assert.Null(result.Error);
        Assert.NotEqual(Guid.Empty, assessment.AssessmentId);
        Assert.Equal(caseId, assessment.CaseId);
        Assert.Equal(complaintText, assessment.ComplaintText);
        Assert.Equal("governance_classifier_v1", assessment.ModelVersion);
        Assert.Equal(ComplaintClassificationCategories.AdministrativeProceduralIntegrity, assessment.PredictedCategory);
        Assert.Equal(ValidProbabilities(), assessment.ClassProbabilities);
        Assert.Equal("Advisory output only.", assessment.AdvisoryNote);
        Assert.Equal("Closed-set taxonomy.", assessment.ClosedSetNote);
        Assert.Equal(FixedUtc, assessment.AssessedAtUtc);

        Assert.Equal(complaintText, client.ComplaintText);
        Assert.Equal(caseId, client.CaseId);
        Assert.Equal(cancellationSource.Token, client.CancellationToken);
        var saved = Assert.Single(store.Assessments);
        Assert.Same(assessment, saved);
        Assert.Equal(cancellationSource.Token, store.CancellationToken);
    }

    [Theory]
    [InlineData(ComplaintClassificationErrorCode.InvalidInput)]
    [InlineData(ComplaintClassificationErrorCode.UpstreamValidationRejected)]
    [InlineData(ComplaintClassificationErrorCode.ServiceUnavailable)]
    [InlineData(ComplaintClassificationErrorCode.Timeout)]
    [InlineData(ComplaintClassificationErrorCode.UpstreamFailure)]
    [InlineData(ComplaintClassificationErrorCode.InvalidResponse)]
    public async Task HandleAsync_ClassifierFailure_ReturnsFailureWithoutPersisting(
        ComplaintClassificationErrorCode errorCode)
    {
        var client = new RecordingClient(_ => ComplaintClassificationResult.Failure(errorCode, "Expected failure."));
        var store = new RecordingStore();
        var handler = CreateHandler(client, store);

        var result = await handler.HandleAsync(
            new ClassifyComplaintCommand("CASE-C4-002", "A sufficiently descriptive complaint."));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Assessment);
        Assert.Equal(errorCode, result.Error?.Code);
        Assert.Empty(store.Assessments);
    }

    [Theory]
    [InlineData("", "A complaint")]
    [InlineData("   ", "A complaint")]
    [InlineData("CASE-1", "")]
    [InlineData("CASE-1", "   ")]
    public async Task HandleAsync_BlankRequiredInput_RejectsBeforeClassifier(
        string caseId,
        string complaintText)
    {
        var client = new RecordingClient(_ => SuccessfulClassification(caseId));
        var store = new RecordingStore();
        var handler = CreateHandler(client, store);

        var result = await handler.HandleAsync(new ClassifyComplaintCommand(caseId, complaintText));

        Assert.False(result.IsSuccess);
        Assert.Equal(ComplaintClassificationErrorCode.InvalidInput, result.Error?.Code);
        Assert.Equal(0, client.CallCount);
        Assert.Empty(store.Assessments);
    }

    [Fact]
    public async Task HandleAsync_InputBeyondLimits_RejectsBeforeClassifier()
    {
        var client = new RecordingClient(_ => SuccessfulClassification("unused"));
        var store = new RecordingStore();
        var handler = CreateHandler(client, store);

        var longCaseResult = await handler.HandleAsync(new ClassifyComplaintCommand(
            new string('C', ComplaintClassificationConstraints.MaximumCaseIdLength + 1),
            "A complaint"));
        var longComplaintResult = await handler.HandleAsync(new ClassifyComplaintCommand(
            "CASE-1",
            string.Concat(Enumerable.Repeat("😀", ComplaintClassificationConstraints.MaximumComplaintTextLength + 1))));

        Assert.Equal(ComplaintClassificationErrorCode.InvalidInput, longCaseResult.Error?.Code);
        Assert.Equal(ComplaintClassificationErrorCode.InvalidInput, longComplaintResult.Error?.Code);
        Assert.Equal(0, client.CallCount);
        Assert.Empty(store.Assessments);
    }

    [Fact]
    public async Task HandleAsync_ClassifierChangesCaseReference_RejectsWithoutPersisting()
    {
        var client = new RecordingClient(_ => SuccessfulClassification("DIFFERENT-CASE"));
        var store = new RecordingStore();
        var handler = CreateHandler(client, store);

        var result = await handler.HandleAsync(
            new ClassifyComplaintCommand("CASE-C4-003", "A complaint"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ComplaintClassificationErrorCode.InvalidResponse, result.Error?.Code);
        Assert.Empty(store.Assessments);
    }

    [Fact]
    public async Task HandleAsync_PersistenceFailure_PropagatesAndDoesNotReturnSuccess()
    {
        var client = new RecordingClient(_ => SuccessfulClassification("CASE-C4-004"));
        var store = new RecordingStore { ExceptionToThrow = new InvalidOperationException("Database unavailable.") };
        var handler = CreateHandler(client, store);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            new ClassifyComplaintCommand("CASE-C4-004", "A complaint")));

        Assert.Equal("Database unavailable.", exception.Message);
        Assert.Single(store.AddAttempts);
        Assert.Empty(store.Assessments);
    }

    [Fact]
    public async Task HandleAsync_RepeatedCommand_AppendsDistinctAssessments()
    {
        const string caseId = "CASE-C4-005";
        var client = new RecordingClient(_ => SuccessfulClassification(caseId));
        var store = new RecordingStore();
        var handler = CreateHandler(client, store);
        var command = new ClassifyComplaintCommand(caseId, "A repeated complaint request.");

        var first = await handler.HandleAsync(command);
        var second = await handler.HandleAsync(command);

        Assert.Equal(2, store.Assessments.Count);
        Assert.NotEqual(first.Assessment?.AssessmentId, second.Assessment?.AssessmentId);
        Assert.All(store.Assessments, assessment => Assert.Equal(caseId, assessment.CaseId));
    }

    [Fact]
    public async Task HandleAsync_PreCancelledToken_PropagatesWithoutCallingDependencies()
    {
        var client = new RecordingClient(_ => SuccessfulClassification("CASE-C4-006"));
        var store = new RecordingStore();
        var handler = CreateHandler(client, store);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.HandleAsync(
            new ClassifyComplaintCommand("CASE-C4-006", "A complaint"),
            cancellationSource.Token));

        Assert.Equal(0, client.CallCount);
        Assert.Empty(store.AddAttempts);
    }

    private static ClassifyComplaintCommandHandler CreateHandler(
        IComplaintClassificationClient client,
        IComplaintClassificationAssessmentStore store) =>
        new(client, store, new FixedTimeProvider(FixedUtc));

    private static ComplaintClassificationResult SuccessfulClassification(string caseId) =>
        ComplaintClassificationResult.Success(new ComplaintClassificationPrediction(
            "governance_classifier_v1",
            ComplaintClassificationCategories.AdministrativeProceduralIntegrity,
            ValidProbabilities(),
            caseId,
            "Advisory output only.",
            "Closed-set taxonomy."));

    private static IReadOnlyDictionary<string, double> ValidProbabilities() =>
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            [ComplaintClassificationCategories.AdministrativeProceduralIntegrity] = 0.7,
            [ComplaintClassificationCategories.LeaseRevenuePaymentEnforcement] = 0.1,
            [ComplaintClassificationCategories.UnauthorizedAllocationTransferUse] = 0.15,
            [ComplaintClassificationCategories.ProtectedEnvironmentalLeaseMisuse] = 0.05
        };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class RecordingClient(
        Func<string?, ComplaintClassificationResult> resultFactory) : IComplaintClassificationClient
    {
        public int CallCount { get; private set; }
        public string? ComplaintText { get; private set; }
        public string? CaseId { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<ComplaintClassificationResult> ClassifyAsync(
            string complaintText,
            string? caseId = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ComplaintText = complaintText;
            CaseId = caseId;
            CancellationToken = cancellationToken;
            return Task.FromResult(resultFactory(caseId));
        }
    }

    private sealed class RecordingStore : IComplaintClassificationAssessmentStore
    {
        public List<ComplaintClassificationAssessment> AddAttempts { get; } = [];
        public List<ComplaintClassificationAssessment> Assessments { get; } = [];
        public Exception? ExceptionToThrow { get; init; }
        public CancellationToken CancellationToken { get; private set; }

        public Task AddAsync(
            ComplaintClassificationAssessment assessment,
            CancellationToken cancellationToken = default)
        {
            AddAttempts.Add(assessment);
            CancellationToken = cancellationToken;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            Assessments.Add(assessment);
            return Task.CompletedTask;
        }

        public Task<ComplaintClassificationAssessment?> GetByIdAsync(
            Guid assessmentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Assessments.SingleOrDefault(item => item.AssessmentId == assessmentId));
    }
}
