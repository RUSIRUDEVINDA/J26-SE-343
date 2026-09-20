namespace StateLandGovernance.UnitTests.WorkflowGovernance.DocumentAnalysisTests;

using System;
using System.Linq;
using Xunit;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

public class VerifiedFactSnapshotContractTests
{
    private readonly DocumentAnalysis _analysis;
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly DateTime _utcNow = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly AnalysisRunId _runId = new AnalysisRunId(Guid.NewGuid());
    private readonly AnalysisRunResultId _resultId = new AnalysisRunResultId(Guid.NewGuid());
    private readonly VerifiedAuthoritySnapshot _authority;
    private readonly ExtractedFactInput _input;

    public VerifiedFactSnapshotContractTests()
    {
        _analysis = new DocumentAnalysis(
            new DocumentAnalysisId(Guid.NewGuid()),
            new GovernedDocumentId(Guid.NewGuid()),
            new DocumentVersionId(Guid.NewGuid()),
            new DocumentChecksum("SHA256", "hash"),
            1, 1, _utcNow.AddDays(-10));

        _analysis.RequestRun(_runId, new AnalysisModelReference("p", "m", "v"), new[] { new AnalysisCapabilityCode("cap") }, _utcNow.AddDays(-9));
        _analysis.StartRun(_runId, _utcNow.AddDays(-8));
        
        _input = new ExtractedFactInput(new ExtractedFactId(Guid.NewGuid()), new FactCode("C1"), new AnalysisFactValue(AnalysisFactValueKind.Text, "Orig"), null, null);
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.OutputsProduced, Array.Empty<AnalysisResultArtifactReference>(), new[] { _input }, _utcNow.AddDays(-7));
        
                        _analysis.RecordFactVerification(new HumanFactVerificationId(Guid.NewGuid()), _resultId, _input.Id, FactVerificationDecision.Confirmed, null, null, _actorId, _utcNow.AddDays(-6), GetAuthority());

        _authority = GetAuthority();
    }

    
    private VerifiedAuthoritySnapshot GetAuthority() => new VerifiedAuthoritySnapshot(_actorId, new[] { "FactSnapshotPublisher", "FactVerifier" }, new AuthorityScope(AuthorityScopeKind.GovernedDocument, _analysis.GovernedDocumentId.Value.ToString("D")), _utcNow.AddDays(-20), _utcNow.AddDays(-10), _utcNow.AddDays(20));
    

    [Fact]
    public void SnapshotProperties_NoPublicOrInitSetters()
    {
        var type = typeof(VerifiedFactSnapshot);
        var props = type.GetProperties();
        foreach (var p in props)
        {
            Assert.True(p.SetMethod == null || !p.SetMethod.IsPublic);
            if (p.SetMethod != null)
                Assert.DoesNotContain(typeof(System.Runtime.CompilerServices.IsExternalInit), p.SetMethod.ReturnParameter.GetRequiredCustomModifiers());
        }
    }

    [Fact]
    public void EntryProperties_NoPublicOrInitSetters()
    {
        var type = typeof(VerifiedFactEntry);
        var props = type.GetProperties();
        foreach (var p in props)
        {
            Assert.True(p.SetMethod == null || !p.SetMethod.IsPublic);
            if (p.SetMethod != null)
                Assert.DoesNotContain(typeof(System.Runtime.CompilerServices.IsExternalInit), p.SetMethod.ReturnParameter.GetRequiredCustomModifiers());
        }
    }

    [Fact]
    public void Collections_CannotBeExternallyMutated()
    {
        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority);
        var snapshot = _analysis.VerifiedFactSnapshots.Single();
        Assert.IsAssignableFrom<System.Collections.ObjectModel.ReadOnlyCollection<VerifiedFactEntry>>(snapshot.Entries);
        Assert.IsAssignableFrom<System.Collections.ObjectModel.ReadOnlyCollection<AnalysisCapabilityCode>>(snapshot.RequestedCapabilities);
        Assert.IsAssignableFrom<System.Collections.ObjectModel.ReadOnlyCollection<VerifiedFactSnapshot>>(_analysis.VerifiedFactSnapshots);
    }

    [Fact]
    public void Snapshot_DoesNotExposeRetainedObjects()
    {
        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority);
        var snapshot = _analysis.VerifiedFactSnapshots.Single();
        var props = snapshot.GetType().GetProperties();
        Assert.DoesNotContain(props, p => p.PropertyType == typeof(AnalysisRun));
        Assert.DoesNotContain(props, p => p.PropertyType == typeof(AnalysisRunResult));
        Assert.DoesNotContain(props, p => p.PropertyType == typeof(ExtractedFact));
        Assert.DoesNotContain(props, p => p.PropertyType == typeof(HumanFactVerification));
    }

    [Fact]
    public void Unsupported_CannotBeConstructedAsEntry()
    {
        var type = typeof(VerifiedFactEntry);
        var ctor = type.GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).First();
        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => ctor.Invoke(new object[] { new ExtractedFactId(Guid.NewGuid()), new HumanFactVerificationId(Guid.NewGuid()), new FactCode("c"), new AnalysisFactValue(AnalysisFactValueKind.Text, "v"), FactVerificationDecision.Unsupported, Guid.NewGuid(), _utcNow }));
        Assert.IsType<InvalidVerifiedFactSnapshotException>(ex.InnerException);
    }

    [Fact]
    public void Success_IncrementsRevisionExactlyOnce()
    {
        var rev = _analysis.Revision;
        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority);
        Assert.Equal(rev + 1, _analysis.Revision);
    }

    [Fact]
    public void Success_RaisesExactlyOneEvent_WithMatchAndNoPayloads()
    {
        var count = _analysis.DomainEvents.Count;
        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority);
        Assert.Equal(count + 1, _analysis.DomainEvents.Count);
        var evt = (VerifiedFactSnapshotPublished)_analysis.DomainEvents.Last();
        Assert.Equal(_utcNow, evt.OccurredOn);
        Assert.Equal(_analysis.Revision, evt.DocumentAnalysisRevision);
        Assert.Equal(26, evt.GetType().GetProperties().Length);
    }

    [Fact]
    public void Failure_Authority_LeavesStateUnchanged()
    {
        var rev = _analysis.Revision;
        var count = _analysis.DomainEvents.Count;
        Assert.Throws<MissingVerifiedAuthorityException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, Guid.NewGuid(), _utcNow, _authority));
        Assert.Equal(rev, _analysis.Revision);
        Assert.Equal(count, _analysis.DomainEvents.Count);
        Assert.Empty(_analysis.VerifiedFactSnapshots);
    }

    [Fact]
    public void Failure_IncompleteVerification_LeavesStateUnchanged()
    {
        var runId2 = new AnalysisRunId(Guid.NewGuid());
        var resId2 = new AnalysisRunResultId(Guid.NewGuid());
        _analysis.RequestRun(runId2, new AnalysisModelReference("p", "m", "v"), new[] { new AnalysisCapabilityCode("cap") }, _utcNow.AddDays(-5));
        _analysis.StartRun(runId2, _utcNow.AddDays(-4));
        _analysis.CompleteRun(runId2, resId2, AnalysisResultOutcome.OutputsProduced, Array.Empty<AnalysisResultArtifactReference>(), new[] { _input }, _utcNow.AddDays(-3));
        
        var rev = _analysis.Revision;
        var count = _analysis.DomainEvents.Count;
        Assert.Throws<IncompleteFactVerificationException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), resId2, _actorId, _utcNow, _authority));
        Assert.Equal(rev, _analysis.Revision);
        Assert.Equal(count, _analysis.DomainEvents.Count);
    }

    [Fact]
    public void Duplicate_ChecksOccurBeforeAlreadyPublished()
    {
        var id = new VerifiedFactSnapshotId(Guid.NewGuid());
        _analysis.PublishVerifiedFactSnapshot(id, _resultId, _actorId, _utcNow, _authority);
        
        var rev = _analysis.Revision;
        var count = _analysis.DomainEvents.Count;
        
        // Exact duplicate ID with EXACT same canonical properties should throw Duplicate
        Assert.Throws<DuplicateVerifiedFactSnapshotException>(() => _analysis.PublishVerifiedFactSnapshot(id, _resultId, _actorId, _utcNow, _authority));
        
        // Exact duplicate ID with DIFFERENT canonical property (PublishedAt) throws Conflicting
        Assert.Throws<ConflictingVerifiedFactSnapshotException>(() => _analysis.PublishVerifiedFactSnapshot(id, _resultId, _actorId, _utcNow.AddMinutes(1), _authority));
        
        // New ID, same result throws AlreadyPublished
        Assert.Throws<VerifiedFactSnapshotAlreadyPublishedException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority));
        
        Assert.Equal(rev, _analysis.Revision);
        Assert.Equal(count, _analysis.DomainEvents.Count);
        Assert.Single(_analysis.VerifiedFactSnapshots);
    }

    [Fact]
    public void Chronology_BeforeRunCompletion_Throws()
    {
        Assert.Throws<InvalidVerifiedFactSnapshotException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow.AddDays(-8), _authority));
    }
    
    [Fact]
    public void Chronology_NonUTC_Throws()
    {
        Assert.Throws<InvalidVerifiedFactSnapshotException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Local), _authority));
    }
    
    [Fact]
    public void Chronology_BeforeVerification_Throws()
    {
        Assert.Throws<InvalidVerifiedFactSnapshotException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow.AddDays(-7), _authority));
    }

    [Fact]
    public void Chronology_BeforeCorrectedVerification_Throws()
    {
        var runId = new AnalysisRunId(Guid.NewGuid());
        var resId = new AnalysisRunResultId(Guid.NewGuid());
        _analysis.RequestRun(runId, new AnalysisModelReference("p", "m", "v"), new[] { new AnalysisCapabilityCode("cap") }, _utcNow.AddDays(-9));
        _analysis.StartRun(runId, _utcNow.AddDays(-8));
        var input = new ExtractedFactInput(new ExtractedFactId(Guid.NewGuid()), new FactCode("C2"), new AnalysisFactValue(AnalysisFactValueKind.Text, "Orig"), null, null);
        _analysis.CompleteRun(runId, resId, AnalysisResultOutcome.OutputsProduced, Array.Empty<AnalysisResultArtifactReference>(), new[] { input }, _utcNow.AddDays(-7));
        
        _analysis.RecordFactVerification(new HumanFactVerificationId(Guid.NewGuid()), resId, input.Id, FactVerificationDecision.Corrected, new AnalysisFactValue(AnalysisFactValueKind.Text, "New"), "A valid reason", _actorId, _utcNow.AddDays(-4), GetAuthority());
        
        Assert.Throws<InvalidVerifiedFactSnapshotException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), resId, _actorId, _utcNow.AddDays(-5), GetAuthority()));
    }
    
    [Fact]
    public void Chronology_BeforeUnsupportedVerification_Throws()
    {
        var runId = new AnalysisRunId(Guid.NewGuid());
        var resId = new AnalysisRunResultId(Guid.NewGuid());
        _analysis.RequestRun(runId, new AnalysisModelReference("p", "m", "v"), new[] { new AnalysisCapabilityCode("cap") }, _utcNow.AddDays(-9));
        _analysis.StartRun(runId, _utcNow.AddDays(-8));
        var input = new ExtractedFactInput(new ExtractedFactId(Guid.NewGuid()), new FactCode("C2"), new AnalysisFactValue(AnalysisFactValueKind.Text, "Orig"), null, null);
        _analysis.CompleteRun(runId, resId, AnalysisResultOutcome.OutputsProduced, Array.Empty<AnalysisResultArtifactReference>(), new[] { input }, _utcNow.AddDays(-7));
        
        _analysis.RecordFactVerification(new HumanFactVerificationId(Guid.NewGuid()), resId, input.Id, FactVerificationDecision.Unsupported, null, "A valid reason", _actorId, _utcNow.AddDays(-4), GetAuthority());
        
        Assert.Throws<InvalidVerifiedFactSnapshotException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), resId, _actorId, _utcNow.AddDays(-5), GetAuthority()));
    }

    [Fact]
    public void SnapshotIdentity_Default_ThrowsAndLeavesStateUnchanged()
    {
        var rev = _analysis.Revision;
        var count = _analysis.DomainEvents.Count;
        Assert.Throws<InvalidVerifiedFactSnapshotException>(() => _analysis.PublishVerifiedFactSnapshot(default, _resultId, _actorId, _utcNow, GetAuthority()));
        Assert.Equal(rev, _analysis.Revision);
        Assert.Equal(count, _analysis.DomainEvents.Count);
        Assert.Empty(_analysis.VerifiedFactSnapshots);
    }

    [Fact]
    public void PrivateRetentionBoundaries_DoesNotRetainExternalObjects()
    {
        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, GetAuthority());
        var snapshot = _analysis.VerifiedFactSnapshots.Single();
        var type = snapshot.GetType();
        
        var fields = type.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        var props = type.GetProperties(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        
        var forbiddenTypes = new[] { typeof(VerifiedAuthoritySnapshot), typeof(AnalysisRun), typeof(AnalysisRunResult), typeof(ExtractedFact), typeof(HumanFactVerification) };
        
        foreach (var field in fields)
        {
            Assert.DoesNotContain(field.FieldType, forbiddenTypes);
        }
        foreach (var prop in props)
        {
            Assert.DoesNotContain(prop.PropertyType, forbiddenTypes);
        }
    }

    [Fact]
    public void CollectionImmutability_ThrowsNotSupportedException()
    {
        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, GetAuthority());
        var snapshot = _analysis.VerifiedFactSnapshots.Single();
        
        var snapshotsList = (System.Collections.Generic.IList<VerifiedFactSnapshot>)_analysis.VerifiedFactSnapshots;
        Assert.Throws<NotSupportedException>(() => snapshotsList.Add(snapshot));
        Assert.Single(_analysis.VerifiedFactSnapshots);
        
        var entriesList = (System.Collections.Generic.IList<VerifiedFactEntry>)snapshot.Entries;
        Assert.Throws<NotSupportedException>(() => entriesList.Clear());
        Assert.Single(snapshot.Entries);
        
        var capsList = (System.Collections.Generic.IList<AnalysisCapabilityCode>)snapshot.RequestedCapabilities;
        Assert.Throws<NotSupportedException>(() => capsList.Clear());
        Assert.Single(snapshot.RequestedCapabilities);
    }

    [Fact]
    public void SnapshotConstructor_NoPublicConstructor()
    {
        var type = typeof(VerifiedFactSnapshot);
        var publicCtors = type.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        Assert.Empty(publicCtors);
    }
    
    [Fact]
    public void SnapshotConstructor_CountProtection_ThrowsViaReflection()
    {
        var type = typeof(VerifiedFactSnapshot);
        var ctor = type.GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).First();
        
        // mismatched counts: sourceFactCount != confirmed + corrected + unsupported
        var args = new object[] {
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentAnalysisId(Guid.NewGuid()), new GovernedDocumentId(Guid.NewGuid()), new DocumentVersionId(Guid.NewGuid()), new DocumentChecksum("A", "V"),
            new AnalysisRunId(Guid.NewGuid()), new AnalysisRunResultId(Guid.NewGuid()), 1, new AnalysisModelReference("p", "m", "v"), new AnalysisCapabilityCode[0],
            AnalysisResultOutcome.OutputsProduced, VerifiedFactSnapshotOutcome.VerifiedFacts, new VerifiedFactEntry[0],
            10 /* source */, 0 /* pub */, 0 /* conf */, 0 /* corr */, 0 /* unsupp */,
            _utcNow, Guid.NewGuid(), "FactSnapshotPublisher", AuthorityScopeKind.GovernedDocument, "1", AuthorityScopeKind.GovernedDocument, "1", _utcNow, _utcNow, _utcNow
        };
        
        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => ctor.Invoke(args));
        Assert.IsType<InvalidVerifiedFactSnapshotException>(ex.InnerException);
    }

    [Fact]
    public void EventPayload_DoesNotExposeComplexTypes()
    {
        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, GetAuthority());
        var evt = _analysis.DomainEvents.OfType<VerifiedFactSnapshotPublished>().Single();
        var type = evt.GetType();
        var props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        
        var forbiddenTypes = new[] { typeof(VerifiedFactSnapshot), typeof(VerifiedFactEntry), typeof(AnalysisRun), typeof(AnalysisRunResult), typeof(ExtractedFact), typeof(HumanFactVerification), typeof(VerifiedAuthoritySnapshot), typeof(object) };
        
        foreach (var prop in props)
        {
            Assert.DoesNotContain(prop.PropertyType, forbiddenTypes);
            Assert.False(prop.PropertyType.Name.Contains("Dictionary") || prop.PropertyType.Name.Contains("dynamic"), $"Property {prop.Name} has invalid type {prop.PropertyType.Name}");
        }
    }
}
