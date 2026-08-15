using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using Xunit;

namespace StateLandGovernance.UnitTests.WorkflowGovernance.DocumentAnalysisTests;

public class DocumentAnalysisTests
{
    private readonly DocumentAnalysisId _validAnalysisId = new(Guid.NewGuid());
    private readonly GovernedDocumentId _validDocId = new(Guid.NewGuid());
    private readonly DocumentVersionId _validVerId = new(Guid.NewGuid());
    private readonly DocumentChecksum _validChecksum = new("SHA256", "checksumvalue123");
    private readonly int _validVerNum = 1;
    private readonly int _validRev = 2;
    private readonly DateTime _createdAt = DateTime.UtcNow;

    private DocumentAnalysis CreateValidAnalysis()
    {
        return new DocumentAnalysis(_validAnalysisId, _validDocId, _validVerId, _validChecksum, _validVerNum, _validRev, _createdAt);
    }

    [Fact]
    public void Create_ValidInputs_InitializesDocumentAnalysis()
    {
        var analysis = CreateValidAnalysis();
        Assert.NotNull(analysis);
    }

    [Fact]
    public void Create_ValidInputs_BindsGovernedDocumentId()
    {
        var analysis = CreateValidAnalysis();
        Assert.Equal(_validDocId, analysis.GovernedDocumentId);
    }

    [Fact]
    public void Create_ValidInputs_BindsDocumentVersionId()
    {
        var analysis = CreateValidAnalysis();
        Assert.Equal(_validVerId, analysis.DocumentVersionId);
    }

    [Fact]
    public void Create_ValidInputs_BindsDocumentChecksum()
    {
        var analysis = CreateValidAnalysis();
        Assert.Equal(_validChecksum, analysis.DocumentChecksum);
    }

    [Fact]
    public void Create_ValidInputs_PreservesDocumentVersionNumber()
    {
        var analysis = CreateValidAnalysis();
        Assert.Equal(_validVerNum, analysis.DocumentVersionNumber);
    }

    [Fact]
    public void Create_ValidInputs_PreservesSourceDocumentRevision()
    {
        var analysis = CreateValidAnalysis();
        Assert.Equal(_validRev, analysis.SourceDocumentRevision);
    }

    [Fact]
    public void Create_ValidInputs_InitializesRevisionToOne()
    {
        var analysis = CreateValidAnalysis();
        Assert.Equal(1, analysis.Revision);
    }

    [Fact]
    public void Create_EmptyAnalysisId_ThrowsInvalidDocumentAnalysis()
    {
        Assert.Throws<InvalidDocumentAnalysisException>(() => new DocumentAnalysis(default, _validDocId, _validVerId, _validChecksum, _validVerNum, _validRev, _createdAt));
    }

    [Fact]
    public void Create_EmptyGovernedDocumentId_ThrowsInvalidDocumentAnalysis()
    {
        Assert.Throws<InvalidDocumentAnalysisException>(() => new DocumentAnalysis(_validAnalysisId, default, _validVerId, _validChecksum, _validVerNum, _validRev, _createdAt));
    }

    [Fact]
    public void Create_EmptyDocumentVersionId_ThrowsInvalidDocumentAnalysis()
    {
        Assert.Throws<InvalidDocumentAnalysisException>(() => new DocumentAnalysis(_validAnalysisId, _validDocId, default, _validChecksum, _validVerNum, _validRev, _createdAt));
    }

    [Fact]
    public void Create_NullChecksum_ThrowsInvalidDocumentAnalysis()
    {
        Assert.Throws<InvalidDocumentAnalysisException>(() => new DocumentAnalysis(_validAnalysisId, _validDocId, _validVerId, null!, _validVerNum, _validRev, _createdAt));
    }

    [Fact]
    public void Create_NonPositiveVersionNumber_ThrowsInvalidDocumentAnalysis()
    {
        Assert.Throws<InvalidDocumentAnalysisException>(() => new DocumentAnalysis(_validAnalysisId, _validDocId, _validVerId, _validChecksum, 0, _validRev, _createdAt));
    }

    [Fact]
    public void Create_NonPositiveSourceRevision_ThrowsInvalidDocumentAnalysis()
    {
        Assert.Throws<InvalidDocumentAnalysisException>(() => new DocumentAnalysis(_validAnalysisId, _validDocId, _validVerId, _validChecksum, _validVerNum, 0, _createdAt));
    }

    [Fact]
    public void Create_NonUtcCreatedAt_ThrowsInvalidDocumentAnalysis()
    {
        Assert.Throws<InvalidDocumentAnalysisException>(() => new DocumentAnalysis(_validAnalysisId, _validDocId, _validVerId, _validChecksum, _validVerNum, _validRev, DateTime.Now));
    }

    [Fact]
    public void Create_Success_RaisesExactlyOneDocumentAnalysisCreatedEvent()
    {
        var analysis = CreateValidAnalysis();
        Assert.Single(analysis.DomainEvents.OfType<DocumentAnalysisCreated>());
    }

    [Fact]
    public void Create_EventContainsExpectedBindingIdentifiers()
    {
        var analysis = CreateValidAnalysis();
        var ev = analysis.DomainEvents.OfType<DocumentAnalysisCreated>().Single();
        Assert.Equal(_validAnalysisId, ev.DocumentAnalysisId);
        Assert.Equal(_validDocId, ev.GovernedDocumentId);
        Assert.Equal(_validVerId, ev.DocumentVersionId);
    }

    [Fact]
    public void Create_EventContainsExpectedChecksumVersionRevisionAndTime()
    {
        var analysis = CreateValidAnalysis();
        var ev = analysis.DomainEvents.OfType<DocumentAnalysisCreated>().Single();
        Assert.Equal(_validChecksum.Algorithm, ev.ChecksumAlgorithm);
        Assert.Equal(_validChecksum.Value, ev.ChecksumValue);
        Assert.Equal(_validVerNum, ev.DocumentVersionNumber);
        Assert.Equal(_validRev, ev.SourceDocumentRevision);
        Assert.Equal(1, ev.DocumentAnalysisRevision);
        Assert.Equal(_createdAt, ev.OccurredOn);
    }

    [Fact]
    public void DomainEvents_ExposedCollection_CannotBeMutated()
    {
        var analysis = CreateValidAnalysis();
        var events = analysis.DomainEvents as IList<IDomainEvent>;
        Assert.Throws<NotSupportedException>(() => events!.Add(null!));
    }

    [Fact]
    public void DocumentBindingProperties_HaveNoPublicSettersOrInitSetters()
    {
        var props = typeof(DocumentAnalysis).GetProperties();
        foreach (var p in props.Where(p => p.Name != "DomainEvents"))
        {
            Assert.False(p.CanWrite && p.SetMethod!.IsPublic);
        }
    }

    [Fact]
    public void DocumentAnalysis_HasNoPublicRebindingMethod()
    {
        var methods = typeof(DocumentAnalysis).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Empty(methods.Where(m => !m.IsSpecialName));
    }

    [Fact]
    public void DocumentAnalysis_DoesNotContainRawDocumentBytes()
    {
        var props = typeof(DocumentAnalysis).GetProperties();
        Assert.DoesNotContain(props, p => p.PropertyType == typeof(byte[]));
    }

    [Fact]
    public void Create_Failure_DoesNotProduceAggregateOrEvent()
    {
        DocumentAnalysis? analysis = null;
        try
        {
            analysis = new DocumentAnalysis(_validAnalysisId, default, _validVerId, _validChecksum, _validVerNum, _validRev, _createdAt);
        }
        catch { }
        
        Assert.Null(analysis);
    }
}
