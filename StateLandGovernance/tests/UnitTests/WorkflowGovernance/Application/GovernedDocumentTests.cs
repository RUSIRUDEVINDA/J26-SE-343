namespace StateLandGovernance.UnitTests.WorkflowGovernance.Application;

using System;
using System.Collections.Generic;
using System.Linq;
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
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Documents.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public class GovernedDocumentTests
{
    private const string ValidSha256V1 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const string ValidSha256V2 = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    private const string ValidSha256V3 = "248d6a61d20638b8e5c026930c3e6039a33ce45964ff2134f1a40d20504620a0";

    private readonly SpyGovernedDocumentRepository _documentRepository;
    private readonly SpyLeaseCaseRepository _leaseCaseRepository;
    private readonly SpyWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly FakeTimeProvider _timeProvider;
    private readonly DateTime _fixedUtcTime;

    private readonly RegisterGovernedDocumentCommandHandler _registerHandler;
    private readonly AddDocumentVersionCommandHandler _addVersionHandler;
    private readonly GetGovernedDocumentByIdQueryHandler _getByIdHandler;
    private readonly GetDocumentsByLeaseCaseIdQueryHandler _getByCaseIdHandler;

    public GovernedDocumentTests()
    {
        _fixedUtcTime = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        _documentRepository = new SpyGovernedDocumentRepository();
        _leaseCaseRepository = new SpyLeaseCaseRepository();
        _unitOfWork = new SpyWorkflowGovernanceUnitOfWork();
        _timeProvider = new FakeTimeProvider(_fixedUtcTime);

        _registerHandler = new RegisterGovernedDocumentCommandHandler(
            _documentRepository,
            _leaseCaseRepository,
            _unitOfWork,
            new RegisterGovernedDocumentCommandValidator(),
            _timeProvider);

        _addVersionHandler = new AddDocumentVersionCommandHandler(
            _documentRepository,
            _unitOfWork,
            new AddDocumentVersionCommandValidator(),
            _timeProvider);

        _getByIdHandler = new GetGovernedDocumentByIdQueryHandler(_documentRepository);
        _getByCaseIdHandler = new GetDocumentsByLeaseCaseIdQueryHandler(_documentRepository, _leaseCaseRepository);
    }

    private static VerifiedAuthorityContext CreateValidAuthorityContext(
        Guid actorId,
        DateTime actionTime,
        string scopeKind = "Global",
        string? targetIdentifier = null,
        string capability = "DocumentSubmitter")
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

    private static VerifiedAuthoritySnapshot CreateValidAuthoritySnapshot(
        Guid actorId,
        DateTime actionTime,
        AuthorityScope? scope = null,
        string capability = "DocumentSubmitter")
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

    private LeaseCase SeedExistingCase(Guid caseId, Guid actorId)
    {
        var caseAuthority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "LeaseInitiator" },
            new AuthorityScope(AuthorityScopeKind.Global, null),
            _fixedUtcTime.AddHours(-1),
            _fixedUtcTime,
            _fixedUtcTime.AddHours(1));

        var leaseCase = new LeaseCase(
            new LeaseCaseId(caseId),
            $"REF-{caseId.ToString()[..8]}",
            actorId,
            _fixedUtcTime,
            caseAuthority);

        _leaseCaseRepository.Seed(leaseCase);
        return leaseCase;
    }

    // --- Document Registration Tests ---

    [Fact]
    public async Task RegisterDocument_ValidCommand_CreatesAggregate_AddsAndCommitsOnce_ReturnsMappedDto()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        SeedExistingCase(caseId, actorId);

        var authority = CreateValidAuthorityContext(actorId, _fixedUtcTime);
        var receipt = DocumentContentReceipt.CreateSha256(
            contentReference: "s3://lease-bucket/docs/survey_2026.pdf",
            sha256Hex: ValidSha256V1,
            originalFileName: "survey_2026.pdf",
            mediaType: "application/pdf",
            fileSizeInBytes: 1024 * 100);

        var command = new RegisterGovernedDocumentCommand(
            LeaseCaseId: caseId,
            LogicalCategory: "SurveyPlan",
            ContentReceipt: receipt,
            ActorId: actorId,
            AuthorityContext: authority);

        // Act
        var result = await _registerHandler.HandleAsync(command);

        // Assert DTO
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(caseId, result.LeaseCaseId);
        Assert.Equal("SurveyPlan", result.LogicalCategory);
        Assert.Equal(1, result.Revision);
        Assert.Equal(1, result.VersionCount);
        Assert.Equal(result.Id, _documentRepository.AddedDocuments[0].Id.Value);

        // Assert Active Version DTO (contains integrity evidence, but no storage secrets)
        Assert.NotNull(result.ActiveVersion);
        Assert.Equal(result.ActiveVersionId, result.ActiveVersion.Id);
        Assert.Equal(1, result.ActiveVersion.VersionNumber);
        Assert.Null(result.ActiveVersion.PredecessorVersionId);
        Assert.Equal("SHA-256", result.ActiveVersion.ChecksumAlgorithm);
        Assert.Equal(ValidSha256V1, result.ActiveVersion.ChecksumValue);
        Assert.Equal("survey_2026.pdf", result.ActiveVersion.OriginalFileName);
        Assert.Equal("application/pdf", result.ActiveVersion.MediaType);
        Assert.Equal(1024 * 100, result.ActiveVersion.FileSizeInBytes);
        Assert.Equal(actorId, result.ActiveVersion.SubmittedByActorId);
        Assert.Equal(_fixedUtcTime, result.ActiveVersion.SubmittedAt);

        // Assert Repository & Unit of Work
        Assert.Single(_documentRepository.AddedDocuments);
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RegisterDocument_MissingLeaseCase_ThrowsLeaseCaseNotFoundException_WithoutAddOrCommit()
    {
        // Arrange: case not seeded in repository
        var nonExistentCaseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var authority = CreateValidAuthorityContext(actorId, _fixedUtcTime);
        var receipt = DocumentContentReceipt.CreateSha256(
            contentReference: "s3://lease-bucket/docs/affidavit.pdf",
            sha256Hex: ValidSha256V1,
            originalFileName: "affidavit.pdf",
            mediaType: "application/pdf",
            fileSizeInBytes: 2048);

        var command = new RegisterGovernedDocumentCommand(
            LeaseCaseId: nonExistentCaseId,
            LogicalCategory: "Affidavit",
            ContentReceipt: receipt,
            ActorId: actorId,
            AuthorityContext: authority);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<LeaseCaseNotFoundException>(() => _registerHandler.HandleAsync(command));
        Assert.Equal(nonExistentCaseId, ex.LeaseCaseId);

        Assert.Empty(_documentRepository.AddedDocuments);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RegisterDocument_NullContentReceipt_ThrowsValidationException_WithoutAddOrCommit()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        SeedExistingCase(caseId, actorId);
        var authority = CreateValidAuthorityContext(actorId, _fixedUtcTime);

        var command = new RegisterGovernedDocumentCommand(
            LeaseCaseId: caseId,
            LogicalCategory: "SurveyPlan",
            ContentReceipt: null!,
            ActorId: actorId,
            AuthorityContext: authority);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _registerHandler.HandleAsync(command));
        Assert.Contains(ex.Errors, e => e.Contains("Content receipt is required"));
        Assert.Empty(_documentRepository.AddedDocuments);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Theory]
    [InlineData("MD5", "Unsupported algorithm MD5")]
    [InlineData("SHA-1", "Unsupported algorithm SHA-1")]
    [InlineData("BLAKE3", "Unsupported algorithm BLAKE3")]
    public async Task RegisterDocument_UnsupportedDigestAlgorithm_ThrowsValidationException_WithoutAddOrCommit(
        string algorithm,
        string scenario)
    {
        // Arrange
        _ = scenario;
        var caseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        SeedExistingCase(caseId, actorId);
        var authority = CreateValidAuthorityContext(actorId, _fixedUtcTime);

        var receipt = new DocumentContentReceipt(
            ContentReference: "s3://bucket/doc.pdf",
            ChecksumAlgorithm: algorithm,
            ChecksumValue: ValidSha256V1,
            OriginalFileName: "doc.pdf",
            MediaType: "application/pdf",
            FileSizeInBytes: 1024);

        var command = new RegisterGovernedDocumentCommand(
            LeaseCaseId: caseId,
            LogicalCategory: "SurveyPlan",
            ContentReceipt: receipt,
            ActorId: actorId,
            AuthorityContext: authority);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _registerHandler.HandleAsync(command));
        Assert.Contains(ex.Errors, e => e.Contains("Checksum algorithm must be 'SHA-256'."));
        Assert.Empty(_documentRepository.AddedDocuments);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Theory]
    [InlineData("not-a-hash", "Arbitrary non-hex string")]
    [InlineData("e3b0c442", "Too short (8 chars)")]
    [InlineData("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b85", "63 chars (off by one)")]
    [InlineData("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855a", "65 chars (off by one)")]
    [InlineData("g3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "Non-hex character 'g'")]
    [InlineData("", "Blank checksum")]
    public async Task RegisterDocument_MalformedSha256_ThrowsValidationException_WithoutAddOrCommit(
        string malformedChecksum,
        string scenario)
    {
        // Arrange
        _ = scenario;
        var caseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        SeedExistingCase(caseId, actorId);
        var authority = CreateValidAuthorityContext(actorId, _fixedUtcTime);

        var receipt = new DocumentContentReceipt(
            ContentReference: "s3://bucket/doc.pdf",
            ChecksumAlgorithm: "SHA-256",
            ChecksumValue: malformedChecksum,
            OriginalFileName: "doc.pdf",
            MediaType: "application/pdf",
            FileSizeInBytes: 1024);

        var command = new RegisterGovernedDocumentCommand(
            LeaseCaseId: caseId,
            LogicalCategory: "SurveyPlan",
            ContentReceipt: receipt,
            ActorId: actorId,
            AuthorityContext: authority);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _registerHandler.HandleAsync(command));
        Assert.Contains(ex.Errors, e => e.Contains("Checksum value"));
        Assert.Empty(_documentRepository.AddedDocuments);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Theory]
    [InlineData("", "application/pdf", 1024, "Filename empty")]
    [InlineData("doc.pdf", "", 1024, "Media type empty")]
    [InlineData("doc.pdf", "application/pdf", 0, "File size zero")]
    [InlineData("doc.pdf", "application/pdf", -5, "File size negative")]
    public async Task RegisterDocument_InvalidReceiptBoundaryInput_ThrowsValidationException_WithoutAddOrCommit(
        string filename,
        string mediaType,
        long fileSize,
        string scenario)
    {
        // Arrange
        _ = scenario;
        var caseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        SeedExistingCase(caseId, actorId);
        var authority = CreateValidAuthorityContext(actorId, _fixedUtcTime);

        var receipt = new DocumentContentReceipt(
            ContentReference: "s3://bucket/doc.pdf",
            ChecksumAlgorithm: "SHA-256",
            ChecksumValue: ValidSha256V1,
            OriginalFileName: filename,
            MediaType: mediaType,
            FileSizeInBytes: fileSize);

        var command = new RegisterGovernedDocumentCommand(
            LeaseCaseId: caseId,
            LogicalCategory: "SurveyPlan",
            ContentReceipt: receipt,
            ActorId: actorId,
            AuthorityContext: authority);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _registerHandler.HandleAsync(command));
        Assert.Empty(_documentRepository.AddedDocuments);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RegisterDocument_Atomicity_InitialVersionAndDocumentRegisteredTogetherInSingleAggregateTransaction()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        SeedExistingCase(caseId, actorId);

        var authority = CreateValidAuthorityContext(actorId, _fixedUtcTime);
        var receipt = DocumentContentReceipt.CreateSha256(
            contentReference: "s3://lease-bucket/docs/plan.pdf",
            sha256Hex: ValidSha256V1,
            originalFileName: "plan.pdf",
            mediaType: "application/pdf",
            fileSizeInBytes: 2048);

        var command = new RegisterGovernedDocumentCommand(
            LeaseCaseId: caseId,
            LogicalCategory: "SurveyPlan",
            ContentReceipt: receipt,
            ActorId: actorId,
            AuthorityContext: authority);

        // Act
        var result = await _registerHandler.HandleAsync(command);

        // Assert: Aggregate contains both DocumentRegistered and DocumentVersionAdded domain events
        Assert.Single(_documentRepository.AddedDocuments);
        var addedAggregate = _documentRepository.AddedDocuments[0];

        Assert.Single(addedAggregate.Versions);
        Assert.Equal(1, addedAggregate.Revision);
        Assert.Contains(addedAggregate.DomainEvents, e => e is DocumentRegistered);
        Assert.Contains(addedAggregate.DomainEvents, e => e is DocumentVersionAdded);

        // Assert: Exactly one commit was issued to the unit of work
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RegisterDocument_DomainAuthorityMissingCapability_PropagatesWithoutAddOrCommit()
    {
        // Arrange: snapshot has "LeaseInitiator" instead of "DocumentSubmitter"
        var caseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        SeedExistingCase(caseId, actorId);

        var wrongAuthority = CreateValidAuthorityContext(
            actorId,
            _fixedUtcTime,
            capability: "LeaseInitiator");

        var receipt = DocumentContentReceipt.CreateSha256(
            contentReference: "s3://bucket/survey.pdf",
            sha256Hex: ValidSha256V1,
            originalFileName: "survey.pdf",
            mediaType: "application/pdf",
            fileSizeInBytes: 4096);

        var command = new RegisterGovernedDocumentCommand(
            LeaseCaseId: caseId,
            LogicalCategory: "SurveyPlan",
            ContentReceipt: receipt,
            ActorId: actorId,
            AuthorityContext: wrongAuthority);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<MissingVerifiedAuthorityException>(() => _registerHandler.HandleAsync(command));
        Assert.Contains("Required capability is missing.", ex.Message);

        Assert.Empty(_documentRepository.AddedDocuments);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    // --- Add Document Version Tests ---

    [Fact]
    public async Task AddVersion_ValidCommand_AppendsVersion_IncrementsSequenceAndRevision_CommitsOnce()
    {
        // Arrange: Seed existing document with version 1
        var caseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        SeedExistingCase(caseId, actorId);

        var docId = new GovernedDocumentId(Guid.NewGuid());
        var initialVersionId = new DocumentVersionId(Guid.NewGuid());
        var domainAuthority = CreateValidAuthoritySnapshot(actorId, _fixedUtcTime);

        var document = new GovernedDocument(
            docId,
            new LeaseCaseId(caseId),
            "SurveyPlan",
            initialVersionId,
            new DocumentChecksum("SHA-256", ValidSha256V1),
            new DocumentContentReference("s3://bucket/v1.pdf"),
            "survey_v1.pdf",
            "application/pdf",
            5000,
            actorId,
            _fixedUtcTime,
            domainAuthority);

        _documentRepository.Seed(document);

        var authorityContext = CreateValidAuthorityContext(actorId, _fixedUtcTime);
        var newVersionId = Guid.NewGuid();
        var receipt = DocumentContentReceipt.CreateSha256(
            contentReference: "s3://bucket/v2.pdf",
            sha256Hex: ValidSha256V2,
            originalFileName: "survey_v2_revised.pdf",
            mediaType: "application/pdf",
            fileSizeInBytes: 6500);

        var command = new AddDocumentVersionCommand(
            GovernedDocumentId: docId.Value,
            ExpectedPredecessorVersionId: initialVersionId.Value,
            ContentReceipt: receipt,
            ActorId: actorId,
            AuthorityContext: authorityContext,
            ExpectedRevision: 1,
            NewVersionId: newVersionId);

        // Act
        var result = await _addVersionHandler.HandleAsync(command);

        // Assert: Aggregate state and version lineage
        Assert.NotNull(result);
        Assert.Equal(2, result.Revision);
        Assert.Equal(2, result.VersionCount);
        Assert.Equal(newVersionId, result.ActiveVersionId);
        Assert.Equal(2, result.ActiveVersion.VersionNumber);
        Assert.Equal(initialVersionId.Value, result.ActiveVersion.PredecessorVersionId);
        Assert.Equal(ValidSha256V2, result.ActiveVersion.ChecksumValue);
        Assert.Equal("survey_v2_revised.pdf", result.ActiveVersion.OriginalFileName);
        Assert.Equal(6500, result.ActiveVersion.FileSizeInBytes);

        // Historical version lineage preserved in append-only history
        Assert.Equal(2, result.Versions.Count);
        Assert.Equal(1, result.Versions[0].VersionNumber);
        Assert.Equal(initialVersionId.Value, result.Versions[0].Id);
        Assert.Equal(2, result.Versions[1].VersionNumber);
        Assert.Equal(newVersionId, result.Versions[1].Id);

        // Unit of work committed once
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AddVersion_MissingDocument_ThrowsGovernedDocumentNotFoundException_WithoutCommit()
    {
        // Arrange
        var missingDocId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var authority = CreateValidAuthorityContext(actorId, _fixedUtcTime);
        var receipt = DocumentContentReceipt.CreateSha256(
            contentReference: "s3://bucket/doc.pdf",
            sha256Hex: ValidSha256V2,
            originalFileName: "doc.pdf",
            mediaType: "application/pdf",
            fileSizeInBytes: 1000);

        var command = new AddDocumentVersionCommand(
            GovernedDocumentId: missingDocId,
            ExpectedPredecessorVersionId: Guid.NewGuid(),
            ContentReceipt: receipt,
            ActorId: actorId,
            AuthorityContext: authority,
            ExpectedRevision: 1);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<GovernedDocumentNotFoundException>(() => _addVersionHandler.HandleAsync(command));
        Assert.Equal(missingDocId, ex.GovernedDocumentId);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AddVersion_StalePredecessor_ThrowsDocumentVersionConflictException_WithoutCommit()
    {
        // Arrange: Document has v1 and v2 already
        var caseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        SeedExistingCase(caseId, actorId);

        var docId = new GovernedDocumentId(Guid.NewGuid());
        var v1Id = new DocumentVersionId(Guid.NewGuid());
        var v2Id = new DocumentVersionId(Guid.NewGuid());
        var domainAuthority = CreateValidAuthoritySnapshot(actorId, _fixedUtcTime);

        var document = new GovernedDocument(
            docId,
            new LeaseCaseId(caseId),
            "SurveyPlan",
            v1Id,
            new DocumentChecksum("SHA-256", ValidSha256V1),
            new DocumentContentReference("s3://bucket/v1.pdf"),
            "v1.pdf",
            "application/pdf",
            1000,
            actorId,
            _fixedUtcTime,
            domainAuthority);

        document.AddVersion(
            v2Id,
            v1Id,
            new DocumentChecksum("SHA-256", ValidSha256V2),
            new DocumentContentReference("s3://bucket/v2.pdf"),
            "v2.pdf",
            "application/pdf",
            1200,
            actorId,
            _fixedUtcTime,
            domainAuthority);

        _documentRepository.Seed(document);

        // Caller attempts to add v3 pointing to stale predecessor v1 instead of active v2
        var authorityContext = CreateValidAuthorityContext(actorId, _fixedUtcTime);
        var receipt = DocumentContentReceipt.CreateSha256(
            contentReference: "s3://bucket/v3.pdf",
            sha256Hex: ValidSha256V3,
            originalFileName: "v3.pdf",
            mediaType: "application/pdf",
            fileSizeInBytes: 1500);

        var command = new AddDocumentVersionCommand(
            GovernedDocumentId: docId.Value,
            ExpectedPredecessorVersionId: v1Id.Value, // Stale predecessor in version lineage
            ContentReceipt: receipt,
            ActorId: actorId,
            AuthorityContext: authorityContext,
            ExpectedRevision: 2);

        // Act & Assert: Domain invariant rejects branching from stale predecessor
        var ex = await Assert.ThrowsAsync<DocumentVersionConflictException>(() => _addVersionHandler.HandleAsync(command));
        Assert.Contains("Predecessor is stale.", ex.Message);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AddVersion_DuplicateChecksum_ThrowsDuplicateDocumentChecksumException_WithoutCommit()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        SeedExistingCase(caseId, actorId);

        var docId = new GovernedDocumentId(Guid.NewGuid());
        var v1Id = new DocumentVersionId(Guid.NewGuid());
        var domainAuthority = CreateValidAuthoritySnapshot(actorId, _fixedUtcTime);

        var document = new GovernedDocument(
            docId,
            new LeaseCaseId(caseId),
            "SurveyPlan",
            v1Id,
            new DocumentChecksum("SHA-256", ValidSha256V1),
            new DocumentContentReference("s3://bucket/v1.pdf"),
            "v1.pdf",
            "application/pdf",
            1000,
            actorId,
            _fixedUtcTime,
            domainAuthority);

        _documentRepository.Seed(document);

        // Caller attempts to add v2 with identical checksum
        var authorityContext = CreateValidAuthorityContext(actorId, _fixedUtcTime);
        var receipt = DocumentContentReceipt.CreateSha256(
            contentReference: "s3://bucket/v2.pdf",
            sha256Hex: ValidSha256V1, // Duplicate of v1
            originalFileName: "v2.pdf",
            mediaType: "application/pdf",
            fileSizeInBytes: 1000);

        var command = new AddDocumentVersionCommand(
            GovernedDocumentId: docId.Value,
            ExpectedPredecessorVersionId: v1Id.Value,
            ContentReceipt: receipt,
            ActorId: actorId,
            AuthorityContext: authorityContext,
            ExpectedRevision: 1);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DuplicateDocumentChecksumException>(() => _addVersionHandler.HandleAsync(command));
        Assert.Contains("Checksum duplicates a historical version.", ex.Message);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AddVersion_StaleExpectedRevision_ThrowsDocumentConcurrencyException_WithoutCommit()
    {
        // Arrange: Document revision is 1
        var caseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        SeedExistingCase(caseId, actorId);

        var docId = new GovernedDocumentId(Guid.NewGuid());
        var v1Id = new DocumentVersionId(Guid.NewGuid());
        var domainAuthority = CreateValidAuthoritySnapshot(actorId, _fixedUtcTime);

        var document = new GovernedDocument(
            docId,
            new LeaseCaseId(caseId),
            "SurveyPlan",
            v1Id,
            new DocumentChecksum("SHA-256", ValidSha256V1),
            new DocumentContentReference("s3://bucket/v1.pdf"),
            "v1.pdf",
            "application/pdf",
            1000,
            actorId,
            _fixedUtcTime,
            domainAuthority);

        _documentRepository.Seed(document);

        // Caller specifies ExpectedRevision = 99 (mismatch)
        var authorityContext = CreateValidAuthorityContext(actorId, _fixedUtcTime);
        var receipt = DocumentContentReceipt.CreateSha256(
            contentReference: "s3://bucket/v2.pdf",
            sha256Hex: ValidSha256V2,
            originalFileName: "v2.pdf",
            mediaType: "application/pdf",
            fileSizeInBytes: 1500);

        var command = new AddDocumentVersionCommand(
            GovernedDocumentId: docId.Value,
            ExpectedPredecessorVersionId: v1Id.Value,
            ContentReceipt: receipt,
            ActorId: actorId,
            AuthorityContext: authorityContext,
            ExpectedRevision: 99);

        // Act & Assert: Application concurrency check throws
        var ex = await Assert.ThrowsAsync<DocumentConcurrencyException>(() => _addVersionHandler.HandleAsync(command));
        Assert.Equal(docId.Value, ex.GovernedDocumentId);
        Assert.Equal(99, ex.ExpectedRevision);
        Assert.Equal(1, ex.ActualRevision);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AddVersion_NonPositiveExpectedRevision_ThrowsValidationException_WithoutCommit(int invalidRevision)
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        SeedExistingCase(caseId, actorId);
        var authorityContext = CreateValidAuthorityContext(actorId, _fixedUtcTime);
        var receipt = DocumentContentReceipt.CreateSha256(
            contentReference: "s3://bucket/v2.pdf",
            sha256Hex: ValidSha256V2,
            originalFileName: "v2.pdf",
            mediaType: "application/pdf",
            fileSizeInBytes: 1500);

        var command = new AddDocumentVersionCommand(
            GovernedDocumentId: Guid.NewGuid(),
            ExpectedPredecessorVersionId: Guid.NewGuid(),
            ContentReceipt: receipt,
            ActorId: actorId,
            AuthorityContext: authorityContext,
            ExpectedRevision: invalidRevision);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _addVersionHandler.HandleAsync(command));
        Assert.Contains(ex.Errors, e => e.Contains("ExpectedRevision must be greater than zero."));
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    // --- Query Tests ---

    [Fact]
    public async Task GetDocumentById_ExistingDocument_ReturnsCorrectlyMappedDto_WithoutStorageSecrets()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var docId = new GovernedDocumentId(Guid.NewGuid());
        var v1Id = new DocumentVersionId(Guid.NewGuid());
        var domainAuthority = CreateValidAuthoritySnapshot(actorId, _fixedUtcTime);

        var document = new GovernedDocument(
            docId,
            new LeaseCaseId(caseId),
            "EnvironmentalAssessment",
            v1Id,
            new DocumentChecksum("SHA-256", ValidSha256V1),
            new DocumentContentReference("s3://internal-secret-bucket/env.pdf"),
            "env_v1.pdf",
            "application/pdf",
            8000,
            actorId,
            _fixedUtcTime,
            domainAuthority);

        _documentRepository.Seed(document);

        // Act
        var result = await _getByIdHandler.HandleAsync(new GetGovernedDocumentByIdQuery(docId.Value));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(docId.Value, result.Id);
        Assert.Equal(caseId, result.LeaseCaseId);
        Assert.Equal("EnvironmentalAssessment", result.LogicalCategory);
        Assert.Equal(v1Id.Value, result.ActiveVersionId);
        Assert.Equal(1, result.Revision);
        Assert.Equal(1, result.VersionCount);
        Assert.Equal(ValidSha256V1, result.ActiveVersion.ChecksumValue);
        Assert.Equal("env_v1.pdf", result.ActiveVersion.OriginalFileName);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task GetDocumentById_MissingDocument_ThrowsGovernedDocumentNotFoundException()
    {
        // Arrange
        var missingId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<GovernedDocumentNotFoundException>(() =>
            _getByIdHandler.HandleAsync(new GetGovernedDocumentByIdQuery(missingId)));

        Assert.Equal(missingId, ex.GovernedDocumentId);
    }

    [Fact]
    public async Task GetDocumentsByLeaseCaseId_ExistingCase_ReturnsAllCaseDocuments()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        SeedExistingCase(caseId, actorId);
        var domainAuthority = CreateValidAuthoritySnapshot(actorId, _fixedUtcTime);

        var doc1 = new GovernedDocument(
            new GovernedDocumentId(Guid.NewGuid()),
            new LeaseCaseId(caseId),
            "SurveyPlan",
            new DocumentVersionId(Guid.NewGuid()),
            new DocumentChecksum("SHA-256", ValidSha256V1),
            new DocumentContentReference("s3://b/1.pdf"),
            "1.pdf",
            "application/pdf",
            100,
            actorId,
            _fixedUtcTime,
            domainAuthority);

        var doc2 = new GovernedDocument(
            new GovernedDocumentId(Guid.NewGuid()),
            new LeaseCaseId(caseId),
            "Affidavit",
            new DocumentVersionId(Guid.NewGuid()),
            new DocumentChecksum("SHA-256", ValidSha256V2),
            new DocumentContentReference("s3://b/2.pdf"),
            "2.pdf",
            "application/pdf",
            200,
            actorId,
            _fixedUtcTime,
            domainAuthority);

        _documentRepository.Seed(doc1);
        _documentRepository.Seed(doc2);

        // Act
        var results = await _getByCaseIdHandler.HandleAsync(new GetDocumentsByLeaseCaseIdQuery(caseId));

        // Assert
        Assert.NotNull(results);
        Assert.Equal(2, results.Count);
        Assert.Contains(results, d => d.LogicalCategory == "SurveyPlan");
        Assert.Contains(results, d => d.LogicalCategory == "Affidavit");
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task GetDocumentsByLeaseCaseId_MissingCase_ThrowsLeaseCaseNotFoundException()
    {
        // Arrange
        var nonExistentCaseId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<LeaseCaseNotFoundException>(() =>
            _getByCaseIdHandler.HandleAsync(new GetDocumentsByLeaseCaseIdQuery(nonExistentCaseId)));

        Assert.Equal(nonExistentCaseId, ex.LeaseCaseId);
    }

    // --- Test Doubles ---

    private sealed class SpyGovernedDocumentRepository : IGovernedDocumentRepository
    {
        private readonly Dictionary<Guid, GovernedDocument> _docsById = new();
        public List<GovernedDocument> AddedDocuments { get; } = new();

        public void Seed(GovernedDocument doc)
        {
            _docsById[doc.Id.Value] = doc;
        }

        public Task<GovernedDocument?> GetByIdAsync(GovernedDocumentId id, CancellationToken cancellationToken = default)
        {
            _docsById.TryGetValue(id.Value, out var doc);
            return Task.FromResult(doc);
        }

        public Task<IReadOnlyList<GovernedDocument>> GetByLeaseCaseIdAsync(LeaseCaseId leaseCaseId, CancellationToken cancellationToken = default)
        {
            var docs = _docsById.Values
                .Where(d => d.LeaseCaseId == leaseCaseId)
                .ToList();
            return Task.FromResult<IReadOnlyList<GovernedDocument>>(docs);
        }

        public Task AddAsync(GovernedDocument document, CancellationToken cancellationToken = default)
        {
            AddedDocuments.Add(document);
            Seed(document);
            return Task.CompletedTask;
        }
    }

    private sealed class SpyLeaseCaseRepository : ILeaseCaseRepository
    {
        private readonly Dictionary<Guid, LeaseCase> _casesById = new();

        public void Seed(LeaseCase leaseCase)
        {
            _casesById[leaseCase.Id.Value] = leaseCase;
        }

        public Task<LeaseCase?> GetByIdAsync(LeaseCaseId id, CancellationToken cancellationToken = default)
        {
            _casesById.TryGetValue(id.Value, out var found);
            return Task.FromResult(found);
        }

        public Task<LeaseCase?> GetByApplicationReferenceAsync(string applicationReference, CancellationToken cancellationToken = default)
        {
            var found = _casesById.Values.FirstOrDefault(c =>
                string.Equals(c.ApplicationReference, applicationReference, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(found);
        }

        public Task AddAsync(LeaseCase leaseCase, CancellationToken cancellationToken = default)
        {
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
