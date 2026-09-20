namespace StateLandGovernance.UnitTests.WorkflowGovernance.Application;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Queries;
using StateLandGovernance.WorkflowGovernance.Application.Validators;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public class CaseIntakeTests
{
    private readonly SpyLeaseCaseRepository _repository;
    private readonly SpyWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly RegisterLeaseCaseCommandValidator _validator;
    private readonly FakeTimeProvider _timeProvider;
    private readonly DateTime _fixedUtcTime;
    private readonly RegisterLeaseCaseCommandHandler _registerHandler;
    private readonly GetLeaseCaseByIdQueryHandler _getQueryHandler;

    public CaseIntakeTests()
    {
        _fixedUtcTime = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        _repository = new SpyLeaseCaseRepository();
        _unitOfWork = new SpyWorkflowGovernanceUnitOfWork();
        _validator = new RegisterLeaseCaseCommandValidator();
        _timeProvider = new FakeTimeProvider(_fixedUtcTime);

        _registerHandler = new RegisterLeaseCaseCommandHandler(
            _repository,
            _unitOfWork,
            _validator,
            _timeProvider);

        _getQueryHandler = new GetLeaseCaseByIdQueryHandler(_repository);
    }

    private static VerifiedAuthoritySnapshot CreateValidAuthoritySnapshot(
        Guid actorId,
        DateTime actionTime,
        AuthorityScope? scope = null,
        string capability = "LeaseInitiator")
    {
        scope ??= new AuthorityScope(AuthorityScopeKind.Global, null);
        return new VerifiedAuthoritySnapshot(
            actorId,
            new[] { capability },
            scope,
            actionTime.AddHours(-1),
            actionTime,
            actionTime.AddHours(1));
    }

    private static VerifiedAuthorityContext CreateValidAuthorityContext(
        Guid actorId,
        DateTime actionTime,
        string scopeKind = "Global",
        string? targetIdentifier = null,
        string capability = "LeaseInitiator")
    {
        return new VerifiedAuthorityContext(
            actorId,
            new List<string> { capability },
            scopeKind,
            targetIdentifier,
            actionTime.AddHours(-1),
            actionTime,
            actionTime.AddHours(1));
    }

    [Fact]
    public async Task RegisterCase_WithValidCommand_CreatesCase_CallsAddAndCommitOnce_ReturnsMappedDto()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var context = CreateValidAuthorityContext(actorId, _fixedUtcTime);
        var command = new RegisterLeaseCaseCommand(
            ApplicationReference: "LND-2026-001",
            ActorId: actorId,
            AuthorityContext: context);

        // Act
        var result = await _registerHandler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("LND-2026-001", result.ApplicationReference);
        Assert.Equal(actorId, result.CreatedByActorId);
        Assert.Equal(_fixedUtcTime, result.CreatedAt);
        Assert.Equal("Draft", result.Status);
        Assert.Equal(1, result.Revision);
        Assert.Null(result.CurrentVerifiedFactSnapshotId);
        Assert.Null(result.ProposalIntake);

        // Repository & Unit of Work assertions
        Assert.Single(_repository.AddedCases);
        Assert.Equal(result.Id, _repository.AddedCases[0].Id.Value);
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RegisterCase_WithProposalIntake_ReturnsMappedDtoWithIntakeSummary()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var context = CreateValidAuthorityContext(actorId, _fixedUtcTime);
        var intakeInput = new ProposalIntakeInput(
            Purpose: "Commercial",
            RequestedExtentValue: 15.5m,
            RequestedExtentUnit: "Acre",
            JurisdictionCode: "WP-COL",
            Region: "Western",
            District: "Colombo",
            SourceReference: "SRC-PORTAL",
            SourceVersion: "1.0");

        var command = new RegisterLeaseCaseCommand(
            ApplicationReference: "LND-2026-002",
            ActorId: actorId,
            AuthorityContext: context,
            ProposalIntake: intakeInput);

        // Act
        var result = await _registerHandler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ProposalIntake);
        Assert.Equal("Commercial", result.ProposalIntake.Purpose);
        Assert.Equal(15.5m, result.ProposalIntake.RequestedExtent);
        Assert.Equal("Acre", result.ProposalIntake.RequestedExtentUnit);
        Assert.Equal("WP-COL", result.ProposalIntake.JurisdictionCode);
        Assert.Equal("SRC-PORTAL", result.ProposalIntake.SourceReference);
        Assert.Equal("1.0", result.ProposalIntake.SourceVersion);
    }

    [Fact]
    public async Task RegisterCase_UsingDomainSnapshotOverload_MapsCorrectly()
    {
        // Arrange: verifies the convenience constructor for callers with Domain snapshots
        var actorId = Guid.NewGuid();
        var snapshot = CreateValidAuthoritySnapshot(actorId, _fixedUtcTime);
        var intake = new LeaseProposalIntake(
            purpose: LeasePurpose.Agricultural,
            requestedExtent: new LandExtent(5m, MeasurementUnit.Hectare),
            jurisdiction: new JurisdictionContext("CP-KAN", "Central", "Kandy"),
            sourceReference: new IntakeSourceReference("PORTAL-V2", "2.0"));

        var command = new RegisterLeaseCaseCommand(
            applicationReference: "LND-2026-SNAPSHOT",
            actorId: actorId,
            authoritySnapshot: snapshot,
            proposalIntake: intake);

        // Act
        var result = await _registerHandler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("LND-2026-SNAPSHOT", result.ApplicationReference);
        Assert.NotNull(result.ProposalIntake);
        Assert.Equal("Agricultural", result.ProposalIntake.Purpose);
        Assert.Equal(5m, result.ProposalIntake.RequestedExtent);
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RegisterCase_DuplicateApplicationReference_ThrowsDuplicateApplicationReferenceException_WithoutAddOrCommit()
    {
        // Arrange: seed existing case
        var actorId = Guid.NewGuid();
        var existingSnapshot = CreateValidAuthoritySnapshot(actorId, _fixedUtcTime);
        var existingCase = new LeaseCase(
            new LeaseCaseId(Guid.NewGuid()),
            "LND-DUP-001",
            actorId,
            _fixedUtcTime,
            existingSnapshot);
        _repository.Seed(existingCase);

        var newActorId = Guid.NewGuid();
        var newContext = CreateValidAuthorityContext(newActorId, _fixedUtcTime);
        var command = new RegisterLeaseCaseCommand(
            ApplicationReference: "  LND-DUP-001  ",
            ActorId: newActorId,
            AuthorityContext: newContext);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DuplicateApplicationReferenceException>(() => _registerHandler.HandleAsync(command));
        Assert.Equal("LND-DUP-001", ex.ApplicationReference);
        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Empty(_repository.AddedCases);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Theory]
    [InlineData("", "ActorValid")]
    [InlineData("   ", "ActorValid")]
    [InlineData(null, "ActorValid")]
    public async Task RegisterCase_InvalidBoundaryApplicationReference_ThrowsValidationException_WithoutAddOrCommit(
        string? invalidReference,
        string scenario)
    {
        // Arrange
        _ = scenario;
        var actorId = Guid.NewGuid();
        var context = CreateValidAuthorityContext(actorId, _fixedUtcTime);
        var command = new RegisterLeaseCaseCommand(
            ApplicationReference: invalidReference!,
            ActorId: actorId,
            AuthorityContext: context);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _registerHandler.HandleAsync(command));
        Assert.Contains("Application reference is required.", ex.Errors);
        Assert.Empty(_repository.AddedCases);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RegisterCase_EmptyActorId_ThrowsValidationException_WithoutAddOrCommit()
    {
        // Arrange
        var context = CreateValidAuthorityContext(Guid.NewGuid(), _fixedUtcTime);
        var command = new RegisterLeaseCaseCommand(
            ApplicationReference: "LND-2026-003",
            ActorId: Guid.Empty,
            AuthorityContext: context);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _registerHandler.HandleAsync(command));
        Assert.Contains("Actor identifier is required.", ex.Errors);
        Assert.Empty(_repository.AddedCases);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RegisterCase_NullAuthorityContext_ThrowsValidationException_WithoutAddOrCommit()
    {
        // Arrange
        var command = new RegisterLeaseCaseCommand(
            ApplicationReference: "LND-2026-004",
            ActorId: Guid.NewGuid(),
            AuthorityContext: null!);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _registerHandler.HandleAsync(command));
        Assert.Contains("Authority context is required.", ex.Errors);
        Assert.Empty(_repository.AddedCases);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RegisterCase_EmptyAuthorityCapabilities_ThrowsValidationException_WithoutAddOrCommit()
    {
        // Arrange: AuthorityContext has empty capabilities
        var actorId = Guid.NewGuid();
        var emptyCapContext = new VerifiedAuthorityContext(
            actorId,
            new List<string>(),
            "Global",
            null,
            _fixedUtcTime.AddHours(-1),
            _fixedUtcTime,
            _fixedUtcTime.AddHours(1));

        var command = new RegisterLeaseCaseCommand(
            ApplicationReference: "LND-2026-NOCAPS",
            ActorId: actorId,
            AuthorityContext: emptyCapContext);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _registerHandler.HandleAsync(command));
        Assert.Contains("Authority context capabilities cannot be null or empty.", ex.Errors);
        Assert.Empty(_repository.AddedCases);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RegisterCase_DomainAuthorityMissingCapability_PropagatesWithoutAddOrCommit()
    {
        // Arrange: snapshot has "ProposalReviewer" instead of "LeaseInitiator"
        var actorId = Guid.NewGuid();
        var context = CreateValidAuthorityContext(
            actorId,
            _fixedUtcTime,
            capability: "ProposalReviewer");

        var command = new RegisterLeaseCaseCommand(
            ApplicationReference: "LND-2026-005",
            ActorId: actorId,
            AuthorityContext: context);

        // Act & Assert: Domain invariant check fails
        var ex = await Assert.ThrowsAsync<MissingVerifiedAuthorityException>(() => _registerHandler.HandleAsync(command));
        Assert.Contains("Required capability is missing.", ex.Message);

        Assert.Empty(_repository.AddedCases);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RegisterCase_DomainAuthorityScopeMismatch_PropagatesWithoutAddOrCommit()
    {
        // Arrange: scope is specific to Case A, but command creates Case B
        var actorId = Guid.NewGuid();
        var caseAId = Guid.NewGuid();
        var caseBId = Guid.NewGuid();
        var scopedContext = CreateValidAuthorityContext(
            actorId,
            _fixedUtcTime,
            scopeKind: "LeaseCase",
            targetIdentifier: caseAId.ToString());

        var command = new RegisterLeaseCaseCommand(
            ApplicationReference: "LND-2026-006",
            ActorId: actorId,
            AuthorityContext: scopedContext,
            LeaseCaseId: caseBId);

        // Act & Assert: Domain constructor rejects because scope does not cover Case B
        var ex = await Assert.ThrowsAsync<MissingVerifiedAuthorityException>(() => _registerHandler.HandleAsync(command));
        Assert.Contains("Required scope is not covered.", ex.Message);

        Assert.Empty(_repository.AddedCases);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task GetCaseById_ExistingCase_ReturnsCorrectlyMappedDto()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var snapshot = CreateValidAuthoritySnapshot(actorId, _fixedUtcTime);
        var existingCase = new LeaseCase(
            new LeaseCaseId(caseId),
            "LND-GET-001",
            actorId,
            _fixedUtcTime,
            snapshot);
        _repository.Seed(existingCase);

        var query = new GetLeaseCaseByIdQuery(caseId);

        // Act
        var result = await _getQueryHandler.HandleAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(caseId, result.Id);
        Assert.Equal("LND-GET-001", result.ApplicationReference);
        Assert.Equal(actorId, result.CreatedByActorId);
        Assert.Equal("Draft", result.Status);
        Assert.Equal(1, result.Revision);
    }

    [Fact]
    public async Task GetCaseById_MissingCase_ThrowsLeaseCaseNotFoundException()
    {
        // Arrange
        var missingId = Guid.NewGuid();
        var query = new GetLeaseCaseByIdQuery(missingId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<LeaseCaseNotFoundException>(() => _getQueryHandler.HandleAsync(query));
        Assert.Equal(missingId, ex.LeaseCaseId);
        Assert.Contains(missingId.ToString(), ex.Message);
    }

    [Fact]
    public async Task GetCaseById_EmptyId_ThrowsValidationException()
    {
        // Arrange
        var query = new GetLeaseCaseByIdQuery(Guid.Empty);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _getQueryHandler.HandleAsync(query));
        Assert.Contains("Lease case identifier is required.", ex.Errors);
    }

    [Fact]
    public async Task GetCaseById_DoesNotMutateAggregate_OrCommitTransaction()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var snapshot = CreateValidAuthoritySnapshot(actorId, _fixedUtcTime);
        var existingCase = new LeaseCase(
            new LeaseCaseId(caseId),
            "LND-QUERY-IMMUTABLE",
            actorId,
            _fixedUtcTime,
            snapshot);
        _repository.Seed(existingCase);

        var initialRevision = existingCase.Revision;
        var query = new GetLeaseCaseByIdQuery(caseId);

        // Act
        var result = await _getQueryHandler.HandleAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(initialRevision, existingCase.Revision);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    // --- Test Doubles ---

    private sealed class SpyLeaseCaseRepository : ILeaseCaseRepository
    {
        private readonly Dictionary<Guid, LeaseCase> _casesById = new();
        private readonly Dictionary<string, LeaseCase> _casesByReference = new(StringComparer.OrdinalIgnoreCase);

        public List<LeaseCase> AddedCases { get; } = new();

        public void Seed(LeaseCase leaseCase)
        {
            _casesById[leaseCase.Id.Value] = leaseCase;
            _casesByReference[leaseCase.ApplicationReference] = leaseCase;
        }

        public Task<LeaseCase?> GetByIdAsync(LeaseCaseId id, CancellationToken cancellationToken = default)
        {
            _casesById.TryGetValue(id.Value, out var found);
            return Task.FromResult(found);
        }

        public Task<LeaseCase?> GetByApplicationReferenceAsync(string applicationReference, CancellationToken cancellationToken = default)
        {
            _casesByReference.TryGetValue(applicationReference, out var found);
            return Task.FromResult(found);
        }

        public Task AddAsync(LeaseCase leaseCase, CancellationToken cancellationToken = default)
        {
            AddedCases.Add(leaseCase);
            Seed(leaseCase);
            return Task.CompletedTask;
        }
    }

    private sealed class SpyWorkflowGovernanceUnitOfWork : IWorkflowGovernanceUnitOfWork
    {
        public int CommitCount { get; private set; }

        public Task<int> CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTime utcNow)
        {
            _utcNow = new DateTimeOffset(utcNow, TimeSpan.Zero);
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
