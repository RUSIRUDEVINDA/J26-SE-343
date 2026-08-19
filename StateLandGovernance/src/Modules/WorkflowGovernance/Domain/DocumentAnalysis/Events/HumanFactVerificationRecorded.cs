namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

public sealed record HumanFactVerificationRecorded(
    Guid EventId,
    DateTime OccurredOn,
    DocumentAnalysisId DocumentAnalysisId,
    AnalysisRunId AnalysisRunId,
    AnalysisRunResultId AnalysisRunResultId,
    HumanFactVerificationId HumanFactVerificationId,
    ExtractedFactId ExtractedFactId,
    string FactCode,
    FactVerificationDecision Decision,
    Guid VerifyingActorId,
    string VerifiedCapability,
    AuthorityScopeKind GrantedAuthorityScopeKind,
    string? GrantedAuthorityScopeIdentifier,
    AuthorityScopeKind RequiredAuthorityScopeKind,
    string RequiredAuthorityScopeIdentifier,
    DateTime AuthorityVerificationTime,
    DocumentVersionId DocumentVersionId,
    string ChecksumAlgorithm,
    string ChecksumValue,
    int RunNumber,
    int DocumentAnalysisRevision
) : IDomainEvent;
