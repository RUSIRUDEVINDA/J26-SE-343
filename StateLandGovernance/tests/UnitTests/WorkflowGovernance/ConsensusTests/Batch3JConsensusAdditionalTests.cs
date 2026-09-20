using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.Events;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace StateLandGovernance.UnitTests.WorkflowGovernance.ConsensusTests;

public class Batch3JConsensusAdditionalTests
{
    private readonly LeaseCaseId _leaseCaseId = new LeaseCaseId(Guid.NewGuid());
    private readonly WorkflowPlanId _planId = new WorkflowPlanId(Guid.NewGuid());
    private readonly WorkflowRuleSetReference _ruleSet = new WorkflowRuleSetReference("RS", "1");
    private readonly DateTime _startedAt = DateTime.UtcNow.AddDays(-10);
    private readonly Guid _officerId = Guid.NewGuid();

    private WorkflowExecution CreateExecution(out WorkflowStageId s1, out WorkflowStageId s2, out WorkflowStageId finalS)
    {
        s1 = new WorkflowStageId(Guid.NewGuid());
        s2 = new WorkflowStageId(Guid.NewGuid());
        finalS = new WorkflowStageId(Guid.NewGuid());
        var code = new WorkflowStageCode("S");
        var inst = new InstitutionCode("INST");
        var d1 = new WorkflowStageDefinition(s1, code, inst, WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
        var d2 = new WorkflowStageDefinition(s2, code, inst, WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
        var df = new WorkflowStageDefinition(finalS, code, inst, WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, new[] { s1, s2 });
        var def = new WorkflowExecutionDefinition(_planId, 1, _leaseCaseId, _ruleSet, _startedAt.AddDays(-1), _startedAt, new[] { d1, d2, df });
        return new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, _startedAt);
    }

    private void CompleteStage(WorkflowExecution exec, WorkflowStageId sId, WorkflowStageDecisionOutcome outcome, string? reason = "R", IEnumerable<string>? conditions = null)
    {
        var auth = new VerifiedInstitutionalAuthoritySnapshot(_officerId, new InstitutionCode("INST"), new[] { "C" }, new AuthorityScope(AuthorityScopeKind.LeaseCase, _leaseCaseId.Value.ToString("D")), _startedAt.AddDays(-1), _startedAt, _startedAt.AddDays(5));
        var dt = _startedAt.AddDays(1);
        exec.StartStage(sId, _officerId, dt, auth);
        IReadOnlyCollection<string>? readonlyConditions = conditions?.ToList().AsReadOnly();
        exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), sId, outcome, reason, readonlyConditions, _officerId, dt.AddHours(1), auth);
    }

    [Fact]
    public void FinalDecision_Abstained_Throws()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Approved);
        
        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });
        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        
        var auth = new VerifiedInstitutionalAuthoritySnapshot(_officerId, new InstitutionCode("INST"), new[] { "C" }, new AuthorityScope(AuthorityScopeKind.LeaseCase, _leaseCaseId.Value.ToString("D")), _startedAt.AddDays(-1), _startedAt, _startedAt.AddDays(5));
        exec.StartStage(finalS, _officerId, _startedAt.AddDays(3), auth);
        
        Assert.Throws<InvalidWorkflowStageTransitionException>(() => exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), finalS, WorkflowStageDecisionOutcome.Abstained, "R", null, _officerId, _startedAt.AddDays(4), auth));
    }
    
    [Fact]
    public void Evaluate_ParticipantCountMismatch_Throws()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Approved);
        
        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true)
        });
        
        Assert.Throws<ConsensusParticipantNotFoundException>(() => exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2)));
    }
    
    [Fact]
    public void Evaluate_FinalStageInPolicyParticipants_Throws()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Approved);
        
        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(finalS, new InstitutionCode("INST"), true, true)
        });
        
        Assert.Throws<ConsensusParticipantNotFoundException>(() => exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2)));
    }

    [Fact]
    public void Evaluate_OutcomeIsRejectionRecommended_WhenBlockingRejectionFound()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Rejected);
        
        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });
        
        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        Assert.Equal(ConsensusOutcome.RejectionRecommended, exec.ConsensusAssessment!.Outcome);
    }
    
    [Fact]
    public void Evaluate_OutcomeIsCorrectionsRequired_WhenChangesRequestedDetected()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.ChangesRequested, "R", new[] { "C1" });
        
        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });
        
        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        Assert.Equal(ConsensusOutcome.CorrectionsRequired, exec.ConsensusAssessment!.Outcome);
    }
    
    [Fact]
    public void Evaluate_OutcomeIsHumanEscalation_WhenMandatoryAbstention()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Abstained);
        
        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, false)
        });
        
        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        Assert.Equal(ConsensusOutcome.HumanEscalationRequired, exec.ConsensusAssessment!.Outcome);
    }
    
    [Fact]
    public void Evaluate_PolicyIdMismatch_Throws()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Approved);
        
        var wrongPlanId = new WorkflowPlanId(Guid.NewGuid());
        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, wrongPlanId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });
        
        Assert.Throws<ConsensusPolicyMismatchException>(() => exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2)));
    }
    
    [Fact]
    public void FinalDecision_RecordedEventContainsConsensusAssessmentId()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Approved);
        
        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });
        
        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        
        var auth = new VerifiedInstitutionalAuthoritySnapshot(_officerId, new InstitutionCode("INST"), new[] { "C" }, new AuthorityScope(AuthorityScopeKind.LeaseCase, _leaseCaseId.Value.ToString("D")), _startedAt.AddDays(-1), _startedAt, _startedAt.AddDays(5));
        exec.StartStage(finalS, _officerId, _startedAt.AddDays(3), auth);
        exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), finalS, WorkflowStageDecisionOutcome.Approved, "R", null, _officerId, _startedAt.AddDays(4), auth);
        
        var eventCompleted = exec.DomainEvents.OfType<WorkflowExecutionCompleted>().First();
        Assert.Equal(exec.ConsensusAssessment!.AssessmentId, eventCompleted.ConsensusAssessmentId);
    }
    
    [Fact]
    public void FinalDecision_CannotBeEmptyReason()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Approved);
        
        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });
        
        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        
        var auth = new VerifiedInstitutionalAuthoritySnapshot(_officerId, new InstitutionCode("INST"), new[] { "C" }, new AuthorityScope(AuthorityScopeKind.LeaseCase, _leaseCaseId.Value.ToString("D")), _startedAt.AddDays(-1), _startedAt, _startedAt.AddDays(5));
        exec.StartStage(finalS, _officerId, _startedAt.AddDays(3), auth);
        
        Assert.Throws<InvalidWorkflowStageTransitionException>(() => exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), finalS, WorkflowStageDecisionOutcome.Approved, "", null, _officerId, _startedAt.AddDays(4), auth));
    }
    
    [Fact]
    public void ParticipantResult_CorrectlyMapped()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Approved);
        
        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), false, false)
        });
        
        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        
        var results = exec.ConsensusAssessment!.ParticipantResults;
        Assert.Equal(2, results.Count);
        
        var r1 = results.First(x => x.WorkflowStageId.Value == s1.Value);
        Assert.True(r1.IsMandatory);
        Assert.True(r1.IsRejectionBlocking);
        Assert.Equal(WorkflowStageDecisionOutcome.Approved, r1.WorkflowStageDecisionOutcome);
        
        var r2 = results.First(x => x.WorkflowStageId.Value == s2.Value);
        Assert.False(r2.IsMandatory);
        Assert.False(r2.IsRejectionBlocking);
    }
    
    [Fact]
    public void Evaluation_MissingParticipant_Throws()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Approved);
        
        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("INST"), true, true)
        });
        
        Assert.Throws<ConsensusParticipantNotFoundException>(() => exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2)));
    }
    
    [Fact]
    public void InvalidConsensusAssessmentException_CanBeThrown()
    {
        var ex = new InvalidConsensusAssessmentException("msg");
        Assert.Equal("msg", ex.Message);
    }
    
    [Fact]
    public void DuplicateConsensusAssessmentException_CanBeThrown()
    {
        var ex = new DuplicateConsensusAssessmentException("msg");
        Assert.Equal("msg", ex.Message);
    }
    
    [Fact]
    public void ConflictingConsensusAssessmentException_CanBeThrown()
    {
        var ex = new ConflictingConsensusAssessmentException("msg");
        Assert.Equal("msg", ex.Message);
    }
    
    [Fact]
    public void ConsensusParticipantNotFoundException_CanBeThrown()
    {
        var ex = new ConsensusParticipantNotFoundException("msg");
        Assert.Equal("msg", ex.Message);
    }
    
    [Fact]
    public void ConsensusPolicyMismatchException_CanBeThrown()
    {
        var ex = new ConsensusPolicyMismatchException("msg");
        Assert.Equal("msg", ex.Message);
    }
    
    [Fact]
    public void InvalidConsensusPolicyException_CanBeThrown()
    {
        var ex = new InvalidConsensusPolicyException("msg");
        Assert.Equal("msg", ex.Message);
    }

    [Fact]
    public void Evaluate_BlockingRejectionAndChangesRequested_YieldsRejectionRecommended()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.ChangesRequested, "R", new[] { "C1" });
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Rejected);
        
        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true) // Blocking rejection
        });
        
        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        Assert.Equal(ConsensusOutcome.RejectionRecommended, exec.ConsensusAssessment!.Outcome);
    }

    [Fact]
    public void Evaluate_PolicyGeneratedAtAfterExecutionStartedAt_ThrowsAtomically()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Approved);
        
        var originalRevision = exec.Revision;
        var originalStatus = exec.Status;
        
        var badPolicy = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt.AddDays(1), new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });
        
        Assert.Throws<InvalidConsensusPolicyException>(() => exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), badPolicy, _startedAt.AddDays(2)));
        
        Assert.Equal(originalRevision, exec.Revision);
        Assert.Equal(originalStatus, exec.Status);
        Assert.Null(exec.ConsensusAssessment);
        Assert.Empty(exec.DomainEvents.OfType<ConsensusAssessmentRecorded>());
    }

    [Fact]
    public void Evaluate_NullPolicy_ThrowsAtomically()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Approved);
        
        var originalRevision = exec.Revision;
        var originalStatus = exec.Status;
        
        Assert.Throws<InvalidConsensusPolicyException>(() => exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), null!, _startedAt.AddDays(2)));
        
        Assert.Equal(originalRevision, exec.Revision);
        Assert.Equal(originalStatus, exec.Status);
        Assert.Null(exec.ConsensusAssessment);
    }

    [Fact]
    public void Evaluate_DuplicateAssessment_DifferentPolicyId_ThrowsConflicting()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Approved);
        
        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });
        var assessmentId = new ConsensusAssessmentId(Guid.NewGuid());
        exec.EvaluateConsensus(assessmentId, p, _startedAt.AddDays(2));
        
        var p2 = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });
        
        Assert.Throws<ConflictingConsensusAssessmentException>(() => exec.EvaluateConsensus(assessmentId, p2, _startedAt.AddDays(2)));
    }

    [Fact]
    public void Evaluate_DuplicateAssessment_DifferentGeneratedAt_ThrowsConflicting()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Approved);
        var pid = new ConsensusPolicyId(Guid.NewGuid());
        var p = new ConsensusPolicySnapshot(pid, "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt.AddHours(-1), new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });
        var assessmentId = new ConsensusAssessmentId(Guid.NewGuid());
        exec.EvaluateConsensus(assessmentId, p, _startedAt.AddDays(2));
        
        var p2 = new ConsensusPolicySnapshot(pid, "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });
        
        Assert.Throws<ConflictingConsensusAssessmentException>(() => exec.EvaluateConsensus(assessmentId, p2, _startedAt.AddDays(2)));
    }
    
    [Fact]
    public void Evaluate_DuplicateAssessment_AfterCompleted_ThrowsDuplicate()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Approved);
        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });
        var assessmentId = new ConsensusAssessmentId(Guid.NewGuid());
        exec.EvaluateConsensus(assessmentId, p, _startedAt.AddDays(2));
        
        var auth = new VerifiedInstitutionalAuthoritySnapshot(_officerId, new InstitutionCode("INST"), new[] { "C" }, new AuthorityScope(AuthorityScopeKind.LeaseCase, _leaseCaseId.Value.ToString("D")), _startedAt.AddDays(-1), _startedAt, _startedAt.AddDays(5));
        exec.StartStage(finalS, _officerId, _startedAt.AddDays(3), auth);
        exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), finalS, WorkflowStageDecisionOutcome.Approved, "R", null, _officerId, _startedAt.AddDays(4), auth);
        
        Assert.Throws<DuplicateConsensusAssessmentException>(() => exec.EvaluateConsensus(assessmentId, p, _startedAt.AddDays(2)));
    }

    [Fact]
    public void Evaluate_DuplicateAssessment_DifferentParticipantOrder_ThrowsDuplicate()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Approved);
        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });
        var assessmentId = new ConsensusAssessmentId(Guid.NewGuid());
        exec.EvaluateConsensus(assessmentId, p, _startedAt.AddDays(2));
        
        var p2 = new ConsensusPolicySnapshot(p.PolicyId, "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true)
        });
        
        Assert.Throws<DuplicateConsensusAssessmentException>(() => exec.EvaluateConsensus(assessmentId, p2, _startedAt.AddDays(2)));
    }
}
