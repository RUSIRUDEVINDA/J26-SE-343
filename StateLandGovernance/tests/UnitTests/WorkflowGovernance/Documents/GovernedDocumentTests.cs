using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Documents.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using Xunit;
using System.Reflection;

namespace StateLandGovernance.UnitTests.WorkflowGovernance.Documents;

public class GovernedDocumentTests
{
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly GovernedDocumentId _validDocId = new(Guid.NewGuid());
    private readonly LeaseCaseId _validCaseId = new(Guid.NewGuid());
    private readonly DocumentVersionId _validVerId = new(Guid.NewGuid());
    private readonly DocumentChecksum _validChecksum = new("SHA256", "checksumvalue123");
    private readonly DocumentContentReference _validRef = new("s3://bucket/obj");
    private readonly DateTime _actionTime = DateTime.UtcNow;
    private readonly VerifiedAuthoritySnapshot _validAuthority;
    private readonly VerifiedAuthoritySnapshot _globalAuthority;

    public GovernedDocumentTests()
    {
        var validFrom = _actionTime.AddMinutes(-5);
        var verificationTime = _actionTime.AddMinutes(-2);
        var validUntil = _actionTime.AddMinutes(10);
        
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, _validCaseId.Value.ToString());
        _validAuthority = new VerifiedAuthoritySnapshot(_actorId, new[] { "DocumentSubmitter" }, scope, validFrom, verificationTime, validUntil);

        var globalScope = new AuthorityScope(AuthorityScopeKind.Global, null);
        _globalAuthority = new VerifiedAuthoritySnapshot(_actorId, new[] { "DocumentSubmitter" }, globalScope, validFrom, verificationTime, validUntil);
    }

    private GovernedDocument CreateValidDocument()
    {
        return new GovernedDocument(_validDocId, _validCaseId, "Title", _validVerId, _validChecksum, _validRef, "file.txt", "text/plain", 1024, _actorId, _actionTime, _validAuthority);
    }

    [Fact]
    public void Register_ValidInputs_CreatesGovernedDocument()
    {
        var doc = CreateValidDocument();
        Assert.Equal(_validDocId, doc.Id);
        Assert.Equal(_validCaseId, doc.LeaseCaseId);
        Assert.Equal("Title", doc.LogicalCategory);
        Assert.Equal(1, doc.Revision);
    }

    [Fact]
    public void Register_ValidInputs_CreatesExactlyOneInitialVersion()
    {
        var doc = CreateValidDocument();
        Assert.Single(doc.Versions);
    }

    [Fact]
    public void Register_InitialVersion_HasVersionNumberOne()
    {
        var doc = CreateValidDocument();
        Assert.Equal(1, doc.Versions.First().VersionNumber);
    }

    [Fact]
    public void Register_InitialVersion_HasNoPredecessor()
    {
        var doc = CreateValidDocument();
        Assert.Null(doc.Versions.First().PredecessorVersionId);
    }

    [Fact]
    public void Register_ActiveVersionId_MatchesInitialVersion()
    {
        var doc = CreateValidDocument();
        Assert.Equal(_validVerId, doc.ActiveVersionId);
    }

    [Fact]
    public void Register_EmptyDocumentId_ThrowsInvalidDocument()
    {
        Assert.Throws<InvalidDocumentException>(() => new GovernedDocument(default, _validCaseId, "Title", _validVerId, _validChecksum, _validRef, "file.txt", "text/plain", 1024, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void Register_EmptyLeaseCaseId_ThrowsInvalidDocument()
    {
        Assert.Throws<InvalidDocumentException>(() => new GovernedDocument(_validDocId, default, "Title", _validVerId, _validChecksum, _validRef, "file.txt", "text/plain", 1024, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void Register_EmptyVersionId_ThrowsInvalidDocument()
    {
        Assert.Throws<InvalidDocumentException>(() => new GovernedDocument(_validDocId, _validCaseId, "Title", default, _validChecksum, _validRef, "file.txt", "text/plain", 1024, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void Register_BlankCategory_ThrowsInvalidDocument()
    {
        Assert.Throws<InvalidDocumentException>(() => new GovernedDocument(_validDocId, _validCaseId, "   ", _validVerId, _validChecksum, _validRef, "file.txt", "text/plain", 1024, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void Register_BlankOriginalFileName_ThrowsInvalidDocument()
    {
        Assert.Throws<InvalidDocumentException>(() => new GovernedDocument(_validDocId, _validCaseId, "Title", _validVerId, _validChecksum, _validRef, "", "text/plain", 1024, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void Register_BlankMediaType_ThrowsInvalidDocument()
    {
        Assert.Throws<InvalidDocumentException>(() => new GovernedDocument(_validDocId, _validCaseId, "Title", _validVerId, _validChecksum, _validRef, "file.txt", "", 1024, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void Register_NonPositiveFileSize_ThrowsInvalidDocument()
    {
        Assert.Throws<InvalidDocumentException>(() => new GovernedDocument(_validDocId, _validCaseId, "Title", _validVerId, _validChecksum, _validRef, "file.txt", "text/plain", 0, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void DocumentChecksum_NullAlgorithm_ThrowsInvalidChecksum()
    {
        Assert.Throws<InvalidChecksumException>(() => new DocumentChecksum(null!, "val"));
    }

    [Fact]
    public void DocumentChecksum_BlankAlgorithm_ThrowsInvalidChecksum()
    {
        Assert.Throws<InvalidChecksumException>(() => new DocumentChecksum("   ", "val"));
    }

    [Fact]
    public void DocumentChecksum_NullValue_ThrowsInvalidChecksum()
    {
        Assert.Throws<InvalidChecksumException>(() => new DocumentChecksum("ALG", null!));
    }

    [Fact]
    public void DocumentChecksum_BlankValue_ThrowsInvalidChecksum()
    {
        Assert.Throws<InvalidChecksumException>(() => new DocumentChecksum("ALG", "  "));
    }

    [Fact]
    public void DocumentChecksum_DifferentlyCasedValues_AreNotEqual()
    {
        var c1 = new DocumentChecksum("SHA", "AbC");
        var c2 = new DocumentChecksum("SHA", "abc");
        Assert.NotEqual(c1, c2);
    }

    [Fact]
    public void DocumentContentReference_BlankValue_ThrowsInvalidContentReference()
    {
        Assert.Throws<InvalidContentReferenceException>(() => new DocumentContentReference("   "));
    }

    [Fact]
    public void Register_EmptyActorId_ThrowsInvalidDocument()
    {
        Assert.Throws<InvalidDocumentException>(() => new GovernedDocument(_validDocId, _validCaseId, "Title", _validVerId, _validChecksum, _validRef, "file.txt", "text/plain", 1024, Guid.Empty, _actionTime, _validAuthority));
    }

    [Fact]
    public void Register_MismatchedAuthorityActor_ThrowsMissingAuthority()
    {
        Assert.Throws<MissingVerifiedAuthorityException>(() => new GovernedDocument(_validDocId, _validCaseId, "Title", _validVerId, _validChecksum, _validRef, "file.txt", "text/plain", 1024, Guid.NewGuid(), _actionTime, _validAuthority));
    }

    [Fact]
    public void Register_MissingDocumentSubmitterCapability_ThrowsMissingAuthority()
    {
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, _validCaseId.Value.ToString());
        var invalidAuthority = new VerifiedAuthoritySnapshot(_actorId, new[] { "OtherCapability" }, scope, _actionTime.AddMinutes(-5), _actionTime.AddMinutes(-2), _actionTime.AddMinutes(10));
        Assert.Throws<MissingVerifiedAuthorityException>(() => new GovernedDocument(_validDocId, _validCaseId, "Title", _validVerId, _validChecksum, _validRef, "file.txt", "text/plain", 1024, _actorId, _actionTime, invalidAuthority));
    }

    [Fact]
    public void Register_MismatchedLeaseCaseScope_ThrowsMissingAuthority()
    {
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, Guid.NewGuid().ToString());
        var mismatchedAuthority = new VerifiedAuthoritySnapshot(_actorId, new[] { "DocumentSubmitter" }, scope, _actionTime.AddMinutes(-5), _actionTime.AddMinutes(-2), _actionTime.AddMinutes(10));
        Assert.Throws<MissingVerifiedAuthorityException>(() => new GovernedDocument(_validDocId, _validCaseId, "Title", _validVerId, _validChecksum, _validRef, "file.txt", "text/plain", 1024, _actorId, _actionTime, mismatchedAuthority));
    }

    [Fact]
    public void Register_GlobalAuthorityScope_Succeeds()
    {
        var doc = new GovernedDocument(_validDocId, _validCaseId, "Title", _validVerId, _validChecksum, _validRef, "file.txt", "text/plain", 1024, _actorId, _actionTime, _globalAuthority);
        Assert.NotNull(doc);
    }

    [Fact]
    public void Register_ActionOutsideAuthorityValidity_ThrowsMissingAuthority()
    {
        Assert.Throws<MissingVerifiedAuthorityException>(() => new GovernedDocument(_validDocId, _validCaseId, "Title", _validVerId, _validChecksum, _validRef, "file.txt", "text/plain", 1024, _actorId, _actionTime.AddMinutes(20), _validAuthority));
    }

    [Fact]
    public void Register_NonUtcActionTime_ThrowsExpectedDomainFailure()
    {
        var localTime = DateTime.Now;
        Assert.Throws<MissingVerifiedAuthorityException>(() => new GovernedDocument(_validDocId, _validCaseId, "Title", _validVerId, _validChecksum, _validRef, "file.txt", "text/plain", 1024, _actorId, localTime, _validAuthority));
    }

    [Fact]
    public void Register_Success_RaisesDocumentRegisteredEvent()
    {
        var doc = CreateValidDocument();
        Assert.Contains(doc.DomainEvents, e => e is DocumentRegistered);
    }

    [Fact]
    public void Register_Success_RaisesDocumentVersionAddedEvent()
    {
        var doc = CreateValidDocument();
        var ev = doc.DomainEvents.OfType<DocumentVersionAdded>().Single();
        Assert.Null(ev.PredecessorVersionId);
    }

    [Fact]
    public void Register_EventsContainExpectedIdentifiersTimeVersionAndRevision()
    {
        var doc = CreateValidDocument();
        var registeredEvent = doc.DomainEvents.OfType<DocumentRegistered>().Single();
        Assert.Equal(_validDocId, registeredEvent.DocumentId);
        Assert.Equal(1, registeredEvent.VersionNumber);
        Assert.Equal(_actionTime, registeredEvent.OccurredOn);
    }

    [Fact]
    public void Versions_ExposedCollection_CannotBeMutated()
    {
        var doc = CreateValidDocument();
        var versions = doc.Versions as IList<DocumentVersion>;
        Assert.NotNull(versions);
        Assert.Throws<NotSupportedException>(() => versions.Add(null!));
    }

    [Fact]
    public void DomainEvents_ExposedCollection_CannotBeMutated()
    {
        var doc = CreateValidDocument();
        var events = doc.DomainEvents as IList<StateLandGovernance.BuildingBlocks.Events.IDomainEvent>;
        Assert.NotNull(events);
        Assert.Throws<NotSupportedException>(() => events.Add(null!));
    }

    [Fact]
    public void DocumentVersion_HasNoPublicMutationPath()
    {
        var props = typeof(DocumentVersion).GetProperties();
        foreach (var p in props)
        {
            Assert.False(p.CanWrite && p.SetMethod!.IsPublic, $"Property {p.Name} should not have a public setter.");
        }
    }

    [Fact]
    public void DocumentVersion_DoesNotContainRawContentBytes()
    {
        var props = typeof(DocumentVersion).GetProperties();
        Assert.DoesNotContain(props, p => p.PropertyType == typeof(byte[]));
    }

    [Fact]
    public void GovernedDocument_DoesNotStoreIsRequired()
    {
        var props = typeof(GovernedDocument).GetProperties();
        Assert.DoesNotContain(props, p => p.Name.Equals("IsRequired", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AddVersion_ValidInputs_AppendsSecondVersion()
    {
        var doc = CreateValidDocument();
        var newVerId = new DocumentVersionId(Guid.NewGuid());
        var newChecksum = new DocumentChecksum("SHA256", "newchecksum");
        
        doc.AddVersion(newVerId, _validVerId, newChecksum, _validRef, "file2.txt", "text/plain", 2048, _actorId, _actionTime, _validAuthority);
        
        Assert.Equal(2, doc.Versions.Count);
        Assert.Equal(newVerId, doc.Versions.Last().Id);
    }

    [Fact]
    public void AddVersion_Success_IncrementsVersionNumber()
    {
        var doc = CreateValidDocument();
        var newVerId = new DocumentVersionId(Guid.NewGuid());
        var newChecksum = new DocumentChecksum("SHA256", "newchecksum");
        
        doc.AddVersion(newVerId, _validVerId, newChecksum, _validRef, "file2.txt", "text/plain", 2048, _actorId, _actionTime, _validAuthority);
        
        Assert.Equal(2, doc.Versions.Last().VersionNumber);
    }

    [Fact]
    public void AddVersion_Success_SetsPredecessorToPreviousActiveVersion()
    {
        var doc = CreateValidDocument();
        var newVerId = new DocumentVersionId(Guid.NewGuid());
        var newChecksum = new DocumentChecksum("SHA256", "newchecksum");
        
        doc.AddVersion(newVerId, _validVerId, newChecksum, _validRef, "file2.txt", "text/plain", 2048, _actorId, _actionTime, _validAuthority);
        
        Assert.Equal(_validVerId, doc.Versions.Last().PredecessorVersionId);
    }

    [Fact]
    public void AddVersion_Success_UpdatesActiveVersionId()
    {
        var doc = CreateValidDocument();
        var newVerId = new DocumentVersionId(Guid.NewGuid());
        var newChecksum = new DocumentChecksum("SHA256", "newchecksum");
        
        doc.AddVersion(newVerId, _validVerId, newChecksum, _validRef, "file2.txt", "text/plain", 2048, _actorId, _actionTime, _validAuthority);
        
        Assert.Equal(newVerId, doc.ActiveVersionId);
    }

    [Fact]
    public void AddVersion_Success_IncrementsDocumentRevisionExactlyOnce()
    {
        var doc = CreateValidDocument();
        int initialRevision = doc.Revision;
        var newVerId = new DocumentVersionId(Guid.NewGuid());
        var newChecksum = new DocumentChecksum("SHA256", "newchecksum");
        
        doc.AddVersion(newVerId, _validVerId, newChecksum, _validRef, "file2.txt", "text/plain", 2048, _actorId, _actionTime, _validAuthority);
        
        Assert.Equal(initialRevision + 1, doc.Revision);
    }

    [Fact]
    public void AddVersion_Success_RaisesExactlyOneNewDocumentVersionAddedEvent()
    {
        var doc = CreateValidDocument();
        int initialEventCount = doc.DomainEvents.Count(e => e is DocumentVersionAdded);
        
        var newVerId = new DocumentVersionId(Guid.NewGuid());
        var newChecksum = new DocumentChecksum("SHA256", "newchecksum");
        
        doc.AddVersion(newVerId, _validVerId, newChecksum, _validRef, "file2.txt", "text/plain", 2048, _actorId, _actionTime, _validAuthority);
        
        var newEvents = doc.DomainEvents.OfType<DocumentVersionAdded>().ToList();
        Assert.Equal(initialEventCount + 1, newEvents.Count);
        var lastEvent = newEvents.Last();
        Assert.Equal(_validVerId, lastEvent.PredecessorVersionId);
    }

    [Fact]
    public void AddVersion_Success_DoesNotRaiseDocumentRegisteredAgain()
    {
        var doc = CreateValidDocument();
        int initialEventCount = doc.DomainEvents.Count(e => e is DocumentRegistered);
        
        var newVerId = new DocumentVersionId(Guid.NewGuid());
        var newChecksum = new DocumentChecksum("SHA256", "newchecksum");
        
        doc.AddVersion(newVerId, _validVerId, newChecksum, _validRef, "file2.txt", "text/plain", 2048, _actorId, _actionTime, _validAuthority);
        
        Assert.Equal(initialEventCount, doc.DomainEvents.Count(e => e is DocumentRegistered));
    }

    [Fact]
    public void AddVersion_Success_PreservesInitialVersionUnchanged()
    {
        var doc = CreateValidDocument();
        var initialVersion = doc.Versions.First();
        
        var newVerId = new DocumentVersionId(Guid.NewGuid());
        var newChecksum = new DocumentChecksum("SHA256", "newchecksum");
        
        doc.AddVersion(newVerId, _validVerId, newChecksum, _validRef, "file2.txt", "text/plain", 2048, _actorId, _actionTime, _validAuthority);
        
        var stillInitial = doc.Versions.First();
        Assert.Same(initialVersion, stillInitial);
    }

    [Fact]
    public void AddVersion_ThirdVersion_FormsCorrectPredecessorChain()
    {
        var doc = CreateValidDocument();
        
        var v2Id = new DocumentVersionId(Guid.NewGuid());
        doc.AddVersion(v2Id, _validVerId, new DocumentChecksum("SHA", "c2"), _validRef, "f2.txt", "text", 10, _actorId, _actionTime, _validAuthority);
        
        var v3Id = new DocumentVersionId(Guid.NewGuid());
        doc.AddVersion(v3Id, v2Id, new DocumentChecksum("SHA", "c3"), _validRef, "f3.txt", "text", 10, _actorId, _actionTime, _validAuthority);
        
        Assert.Equal(3, doc.Versions.Count);
        var v3 = doc.Versions.Last();
        Assert.Equal(v2Id, v3.PredecessorVersionId);
        Assert.Equal(3, v3.VersionNumber);
    }

    [Fact]
    public void AddVersion_EmptyVersionId_ThrowsInvalidDocument()
    {
        var doc = CreateValidDocument();
        Assert.Throws<InvalidDocumentException>(() => doc.AddVersion(default, _validVerId, new DocumentChecksum("SHA", "c2"), _validRef, "f2", "t", 10, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void AddVersion_DuplicateVersionId_ThrowsDuplicateVersion()
    {
        var doc = CreateValidDocument();
        Assert.Throws<DuplicateDocumentVersionException>(() => doc.AddVersion(_validVerId, _validVerId, new DocumentChecksum("SHA", "c2"), _validRef, "f2", "t", 10, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void AddVersion_MissingPredecessor_ThrowsVersionConflict()
    {
        var doc = CreateValidDocument();
        Assert.Throws<DocumentVersionConflictException>(() => doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), default, new DocumentChecksum("SHA", "c2"), _validRef, "f2", "t", 10, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void AddVersion_StalePredecessor_ThrowsVersionConflict()
    {
        var doc = CreateValidDocument();
        var v2Id = new DocumentVersionId(Guid.NewGuid());
        doc.AddVersion(v2Id, _validVerId, new DocumentChecksum("SHA", "c2"), _validRef, "f2", "t", 10, _actorId, _actionTime, _validAuthority);
        
        var v3Id = new DocumentVersionId(Guid.NewGuid());
        Assert.Throws<DocumentVersionConflictException>(() => doc.AddVersion(v3Id, _validVerId, new DocumentChecksum("SHA", "c3"), _validRef, "f3", "t", 10, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void AddVersion_PredecessorNotInCollection_ThrowsVersionConflict()
    {
        var doc = CreateValidDocument();
        var fakeId = new DocumentVersionId(Guid.NewGuid());
        Assert.Throws<DocumentVersionConflictException>(() => doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), fakeId, new DocumentChecksum("SHA", "c2"), _validRef, "f2", "t", 10, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void AddVersion_DuplicateInitialChecksum_ThrowsDuplicateChecksum()
    {
        var doc = CreateValidDocument();
        Assert.Throws<DuplicateDocumentChecksumException>(() => doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, _validChecksum, _validRef, "f2", "t", 10, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void AddVersion_DuplicateOlderHistoricalChecksum_ThrowsDuplicateChecksum()
    {
        var doc = CreateValidDocument();
        var v2Id = new DocumentVersionId(Guid.NewGuid());
        doc.AddVersion(v2Id, _validVerId, new DocumentChecksum("SHA", "c2"), _validRef, "f2", "t", 10, _actorId, _actionTime, _validAuthority);
        
        Assert.Throws<DuplicateDocumentChecksumException>(() => doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), v2Id, _validChecksum, _validRef, "f3", "t", 10, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void AddVersion_MismatchedActor_ThrowsMissingAuthority()
    {
        var doc = CreateValidDocument();
        Assert.Throws<MissingVerifiedAuthorityException>(() => doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, new DocumentChecksum("SHA", "c2"), _validRef, "f2", "t", 10, Guid.NewGuid(), _actionTime, _validAuthority));
    }

    [Fact]
    public void AddVersion_MissingDocumentSubmitterCapability_ThrowsMissingAuthority()
    {
        var doc = CreateValidDocument();
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, _validCaseId.Value.ToString());
        var invalidAuthority = new VerifiedAuthoritySnapshot(_actorId, new[] { "Other" }, scope, _actionTime.AddMinutes(-5), _actionTime, _actionTime.AddMinutes(10));
        
        Assert.Throws<MissingVerifiedAuthorityException>(() => doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, new DocumentChecksum("SHA", "c2"), _validRef, "f2", "t", 10, _actorId, _actionTime, invalidAuthority));
    }

    [Fact]
    public void AddVersion_MismatchedLeaseCaseScope_ThrowsMissingAuthority()
    {
        var doc = CreateValidDocument();
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, Guid.NewGuid().ToString());
        var invalidAuthority = new VerifiedAuthoritySnapshot(_actorId, new[] { "DocumentSubmitter" }, scope, _actionTime.AddMinutes(-5), _actionTime, _actionTime.AddMinutes(10));
        
        Assert.Throws<MissingVerifiedAuthorityException>(() => doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, new DocumentChecksum("SHA", "c2"), _validRef, "f2", "t", 10, _actorId, _actionTime, invalidAuthority));
    }

    [Fact]
    public void AddVersion_GlobalAuthorityScope_Succeeds()
    {
        var doc = CreateValidDocument();
        doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, new DocumentChecksum("SHA", "c2"), _validRef, "f2", "t", 10, _actorId, _actionTime, _globalAuthority);
        Assert.Equal(2, doc.Versions.Count);
    }

    [Fact]
    public void AddVersion_ActionBeforeValidity_ThrowsMissingAuthority()
    {
        var doc = CreateValidDocument();
        Assert.Throws<MissingVerifiedAuthorityException>(() => doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, new DocumentChecksum("SHA", "c2"), _validRef, "f2", "t", 10, _actorId, _actionTime.AddMinutes(-10), _validAuthority));
    }

    [Fact]
    public void AddVersion_ActionAfterValidity_ThrowsMissingAuthority()
    {
        var doc = CreateValidDocument();
        Assert.Throws<MissingVerifiedAuthorityException>(() => doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, new DocumentChecksum("SHA", "c2"), _validRef, "f2", "t", 10, _actorId, _actionTime.AddMinutes(20), _validAuthority));
    }

    [Fact]
    public void AddVersion_NonUtcActionTime_ThrowsExpectedDomainFailure()
    {
        var doc = CreateValidDocument();
        Assert.Throws<InvalidDocumentException>(() => doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, new DocumentChecksum("SHA", "c2"), _validRef, "f2", "t", 10, _actorId, DateTime.Now, _validAuthority));
    }

    [Fact]
    public void AddVersion_BlankFileName_ThrowsInvalidDocument()
    {
        var doc = CreateValidDocument();
        Assert.Throws<InvalidDocumentException>(() => doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, new DocumentChecksum("SHA", "c2"), _validRef, "  ", "t", 10, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void AddVersion_BlankMediaType_ThrowsInvalidDocument()
    {
        var doc = CreateValidDocument();
        Assert.Throws<InvalidDocumentException>(() => doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, new DocumentChecksum("SHA", "c2"), _validRef, "f2", "  ", 10, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void AddVersion_NonPositiveFileSize_ThrowsInvalidDocument()
    {
        var doc = CreateValidDocument();
        Assert.Throws<InvalidDocumentException>(() => doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, new DocumentChecksum("SHA", "c2"), _validRef, "f2", "t", 0, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void AddVersion_InvalidChecksum_ThrowsExpectedFailure()
    {
        var doc = CreateValidDocument();
        Assert.Throws<InvalidDocumentException>(() => doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, null!, _validRef, "f2", "t", 10, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void AddVersion_InvalidContentReference_ThrowsExpectedFailure()
    {
        var doc = CreateValidDocument();
        Assert.Throws<InvalidDocumentException>(() => doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, new DocumentChecksum("SHA", "c2"), null!, "f2", "t", 10, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void AddVersion_Failure_DoesNotChangeVersionCollection()
    {
        var doc = CreateValidDocument();
        try { doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, _validChecksum, _validRef, "f2", "t", 10, _actorId, _actionTime, _validAuthority); } catch { }
        Assert.Single(doc.Versions);
    }

    [Fact]
    public void AddVersion_Failure_DoesNotChangeActiveVersionId()
    {
        var doc = CreateValidDocument();
        try { doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, _validChecksum, _validRef, "f2", "t", 10, _actorId, _actionTime, _validAuthority); } catch { }
        Assert.Equal(_validVerId, doc.ActiveVersionId);
    }

    [Fact]
    public void AddVersion_Failure_DoesNotIncrementRevision()
    {
        var doc = CreateValidDocument();
        int initialRev = doc.Revision;
        try { doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, _validChecksum, _validRef, "f2", "t", 10, _actorId, _actionTime, _validAuthority); } catch { }
        Assert.Equal(initialRev, doc.Revision);
    }

    [Fact]
    public void AddVersion_Failure_DoesNotRaiseNewEvent()
    {
        var doc = CreateValidDocument();
        int initialCount = doc.DomainEvents.Count;
        try { doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, _validChecksum, _validRef, "f2", "t", 10, _actorId, _actionTime, _validAuthority); } catch { }
        Assert.Equal(initialCount, doc.DomainEvents.Count);
    }

    [Fact]
    public void Versions_HistoricalEntries_RemainExternallyImmutable()
    {
        var doc = CreateValidDocument();
        doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, new DocumentChecksum("SHA", "c2"), _validRef, "f2", "t", 10, _actorId, _actionTime, _validAuthority);
        var firstVersion = doc.Versions.First();
        var props = firstVersion.GetType().GetProperties();
        foreach (var p in props)
        {
            Assert.False(p.CanWrite && p.SetMethod!.IsPublic);
        }
    }

    [Fact]
    public void Versions_Collection_RemainsExternallyReadOnly()
    {
        var doc = CreateValidDocument();
        doc.AddVersion(new DocumentVersionId(Guid.NewGuid()), _validVerId, new DocumentChecksum("SHA", "c2"), _validRef, "f2", "t", 10, _actorId, _actionTime, _validAuthority);
        var versions = doc.Versions as IList<DocumentVersion>;
        Assert.Throws<NotSupportedException>(() => versions!.Add(null!));
    }

    [Fact]
    public void CalculateNextVersionNumber_NormalValues_ReturnsIncremented()
    {
        Assert.Equal(2, GovernedDocument.CalculateNextVersionNumber(1));
        Assert.Equal(10, GovernedDocument.CalculateNextVersionNumber(9));
    }

    [Fact]
    public void CalculateNextVersionNumber_NegativeOrZero_ThrowsInvalidDocumentException()
    {
        Assert.Throws<InvalidDocumentException>(() => GovernedDocument.CalculateNextVersionNumber(0));
        Assert.Throws<InvalidDocumentException>(() => GovernedDocument.CalculateNextVersionNumber(-1));
    }

    [Fact]
    public void CalculateNextVersionNumber_IntMaxValue_ThrowsDocumentVersionNumberOverflowException()
    {
        Assert.Throws<DocumentVersionNumberOverflowException>(() => GovernedDocument.CalculateNextVersionNumber(int.MaxValue));
    }
}
