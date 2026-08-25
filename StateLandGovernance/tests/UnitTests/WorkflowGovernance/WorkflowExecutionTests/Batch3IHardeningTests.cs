using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.Events;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using Xunit;

namespace StateLandGovernance.UnitTests.WorkflowGovernance.WorkflowExecutionTests
{
    public class Batch3IHardeningTests
    {
        private readonly Guid Actor = Guid.NewGuid();
        private readonly InstitutionCode Inst = new InstitutionCode("I");
        private readonly LeaseCaseId LcId = new LeaseCaseId(Guid.NewGuid());
        private AuthorityScope Scope => new AuthorityScope(AuthorityScopeKind.LeaseCase, LcId.Value.ToString("D"));
        private readonly DateTime Now = DateTime.UtcNow;
        private VerifiedInstitutionalAuthoritySnapshot ValidAuth => new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "C" }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5));

        // Authority
        [Fact]
        public void Constructor_Capability_IsTrimmed()
        {
            var auth = new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "  Cap  " }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5));
            Assert.Contains("Cap", auth.Capabilities);
            Assert.DoesNotContain("  Cap  ", auth.Capabilities);
        }

        [Fact]
        public void Constructor_WhitespaceVariantDuplicateCapability_Throws()
        {
            Assert.Throws<InvalidAuthoritySnapshotException>(() => new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "Cap", " Cap " }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5)));
        }

        [Fact]
        public void Constructor_OverlengthCapability_Throws()
        {
            var longCap = new string('A', 101);
            Assert.Throws<InvalidAuthoritySnapshotException>(() => new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { longCap }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5)));
        }

        [Fact]
        public void EnsureAuthorizes_UsesCanonicalCapability()
        {
            var auth = new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "  Cap  " }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5));
            auth.EnsureAuthorizes(Actor, Inst, "Cap", Scope, Now);
        }

        [Fact]
        public void Capabilities_CannotBeExternallyMutated()
        {
            var caps = new[] { "C1" };
            var auth = new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, caps, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5));
            caps[0] = "C2";
            Assert.Contains("C1", auth.Capabilities);
            Assert.DoesNotContain("C2", auth.Capabilities);
        }

        // Definitions
        [Fact]
        public void Definition_DefaultRuleSetReference_Throws()
        {
            Assert.Throws<InvalidWorkflowExecutionException>(() => new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, LcId, default, Now.AddDays(-1), Now, new[] { new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>()) }));
        }

        [Fact]
        public void StageDefinition_DefaultRoutingReasonCode_Throws()
        {
            Assert.Throws<InvalidWorkflowExecutionException>(() => new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", default, "D", null, true, Array.Empty<WorkflowStageId>()));
        }

        [Fact]
        public void StageDefinition_MissingReasonDescription_Throws()
        {
            Assert.Throws<InvalidWorkflowExecutionException>(() => new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), " ", null, true, Array.Empty<WorkflowStageId>()));
        }

        [Fact]
        public void Definition_NullStageElement_Throws()
        {
            Assert.Throws<InvalidWorkflowExecutionException>(() => new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, LcId, new WorkflowRuleSetReference("R", "1"), Now.AddDays(-1), Now, new WorkflowStageDefinition[] { null! }));
        }

        [Fact]
        public void Definition_MultipleFinalStages_Throws()
        {
            var sf1 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F1"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>());
            var sf2 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F2"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>());
            Assert.Throws<InvalidWorkflowExecutionException>(() => new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, LcId, new WorkflowRuleSetReference("R", "1"), Now.AddDays(-1), Now, new[] { sf1, sf2 }));
        }

        [Fact]
        public void StageDefinition_FinalMarkerTypeMismatch_Throws()
        {
            Assert.Throws<InvalidWorkflowExecutionException>(() => new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>()));
        }

        [Fact]
        public void StageDefinition_DuplicatePrerequisites_Throws()
        {
            var p = new WorkflowStageId(Guid.NewGuid());
            Assert.Throws<InvalidWorkflowExecutionException>(() => new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, new[] { p, p }));
        }

        [Fact]
        public void Definition_CollectionsCannotBeExternallyMutated()
        {
            var sf = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>());
            var arr = new[] { sf };
            var def = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, LcId, new WorkflowRuleSetReference("R", "1"), Now.AddDays(-1), Now, arr);
            arr[0] = null!;
            Assert.NotNull(def.Stages.First());
        }

        [Fact]
        public void StageDefinition_PrerequisitesCannotBeExternallyMutated()
        {
            var p = new[] { new WorkflowStageId(Guid.NewGuid()) };
            var sf = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, p);
            p[0] = new WorkflowStageId(Guid.NewGuid());
            Assert.NotEqual(p[0], sf.Prerequisites.First());
        }

        // Identity and revision
        [Fact]
        public void WorkflowExecution_DefaultId_IsRejectedAtBoundary()
        {
            var sf = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>());
            var def = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, LcId, new WorkflowRuleSetReference("R", "1"), Now.AddDays(-1), Now, new[] { sf });
            Assert.Throws<InvalidWorkflowExecutionException>(() => new WorkflowExecution(default, def, Now));
        }

        [Fact]
        public void WorkflowStageDecision_DefaultId_IsRejectedAtBoundary()
        {
            var sf = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
            var sf2 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>());
            var def = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, LcId, new WorkflowRuleSetReference("R", "1"), Now.AddDays(-1), Now, new[] { sf, sf2 });
            var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, Now);
            exec.StartStage(sf.StageId, Actor, Now, ValidAuth);
            Assert.Throws<InvalidWorkflowStageDecisionException>(() => exec.RecordStageDecision(default, sf.StageId, WorkflowStageDecisionOutcome.Approved, null, null, Actor, Now, ValidAuth));
        }

        [Fact]
        public void CalculateNextRevision_IntMaxValue_ThrowsWorkflowExecutionRevisionOverflowException()
        {
            Assert.Throws<WorkflowExecutionRevisionOverflowException>(() => WorkflowExecution.CalculateNextRevision(int.MaxValue));
        }

        [Fact]
        public void CalculateNextRevision_NormalValue_ReturnsIncrement()
        {
            Assert.Equal(2, WorkflowExecution.CalculateNextRevision(1));
        }

        [Fact]
        public void WorkflowExecutionStarted_ExactPropertyTypeMap()
        {
            var expected = new Dictionary<string, Type>
            {
                { "EventId", typeof(Guid) },
                { "OccurredOn", typeof(DateTime) },
                { "WorkflowExecutionId", typeof(WorkflowExecutionId) },
                { "WorkflowPlanId", typeof(WorkflowPlanId) },
                { "WorkflowPlanRevision", typeof(int) },
                { "LeaseCaseId", typeof(LeaseCaseId) },
                { "InitialReadyStageCount", typeof(int) },
                { "WorkflowExecutionRevision", typeof(int) }
            };
            var actual = typeof(WorkflowExecutionStarted).GetProperties().ToDictionary(p => p.Name, p => p.PropertyType);
            Assert.Equal(expected.Count, actual.Count);
            foreach (var kvp in expected)
            {
                Assert.True(actual.ContainsKey(kvp.Key), $"Missing property {kvp.Key}");
                Assert.Equal(kvp.Value, actual[kvp.Key]);
            }
        }

        [Fact]
        public void WorkflowStageStarted_ExactPropertyTypeMap()
        {
            var expected = new Dictionary<string, Type>
            {
                { "EventId", typeof(Guid) },
                { "OccurredOn", typeof(DateTime) },
                { "WorkflowExecutionId", typeof(WorkflowExecutionId) },
                { "WorkflowPlanId", typeof(WorkflowPlanId) },
                { "WorkflowStageId", typeof(WorkflowStageId) },
                { "WorkflowStageCode", typeof(WorkflowStageCode) },
                { "InstitutionCode", typeof(InstitutionCode) },
                { "ActingOfficerId", typeof(Guid) },
                { "WorkflowExecutionRevision", typeof(int) }
            };
            var actual = typeof(WorkflowStageStarted).GetProperties().ToDictionary(p => p.Name, p => p.PropertyType);
            Assert.Equal(expected.Count, actual.Count);
            foreach (var kvp in expected)
            {
                Assert.True(actual.ContainsKey(kvp.Key), $"Missing property {kvp.Key}");
                Assert.Equal(kvp.Value, actual[kvp.Key]);
            }
        }

        [Fact]
        public void WorkflowStageDecisionRecorded_ExactPropertyTypeMap()
        {
            var expected = new Dictionary<string, Type>
            {
                { "EventId", typeof(Guid) },
                { "OccurredOn", typeof(DateTime) },
                { "WorkflowExecutionId", typeof(WorkflowExecutionId) },
                { "WorkflowPlanId", typeof(WorkflowPlanId) },
                { "WorkflowStageId", typeof(WorkflowStageId) },
                { "WorkflowStageDecisionId", typeof(WorkflowStageDecisionId) },
                { "InstitutionCode", typeof(InstitutionCode) },
                { "Outcome", typeof(WorkflowStageDecisionOutcome) },
                { "DecidingOfficerId", typeof(Guid) },
                { "NewlyReadyStageCount", typeof(int) },
                { "WorkflowExecutionStatus", typeof(WorkflowExecutionStatus) },
                { "WorkflowExecutionRevision", typeof(int) }
            };
            var actual = typeof(WorkflowStageDecisionRecorded).GetProperties().ToDictionary(p => p.Name, p => p.PropertyType);
            Assert.Equal(expected.Count, actual.Count);
            foreach (var kvp in expected)
            {
                Assert.True(actual.ContainsKey(kvp.Key), $"Missing property {kvp.Key}");
                Assert.Equal(kvp.Value, actual[kvp.Key]);
            }
        }

        [Fact]
        public void DecisionEvent_DoesNotExposeReasonConditionsAuthorityOrFullStage()
        {
            var props = typeof(WorkflowStageDecisionRecorded).GetProperties();
            Assert.DoesNotContain(props, p => p.Name == "Reason" || p.Name == "Conditions" || p.Name == "AuthoritySnapshot" || p.Name == "Stage");
        }

        [Fact]
        public void WorkflowExecution_HasNoPublicSettersOrRebindingMethods()
        {
            var props = typeof(WorkflowExecution).GetProperties();
            Assert.All(props, p => Assert.Null(p.GetSetMethod()));
        }

        [Fact]
        public void WorkflowStageExecution_HasNoPublicSetters()
        {
            var props = typeof(WorkflowStageExecution).GetProperties();
            Assert.All(props, p => Assert.Null(p.GetSetMethod()));
        }

        [Fact]
        public void WorkflowExecution_DoesNotRetainAuthoritySnapshot()
        {
            var fields = typeof(WorkflowExecution).GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.DoesNotContain(fields, f => f.FieldType == typeof(VerifiedInstitutionalAuthoritySnapshot));
        }

        [Fact]
        public void RecordDecision_InvalidCondition_LeavesAggregateUnchanged()
        {
            var s1 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
            var s2 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>());
            var def = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, LcId, new WorkflowRuleSetReference("R", "1"), Now.AddDays(-1), Now, new[] { s1, s2 });
            var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, Now);
            
            exec.StartStage(s1.StageId, Actor, Now, ValidAuth);
            var originalRevision = exec.Revision;
            
            Assert.Throws<InvalidWorkflowStageDecisionException>(() => exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), s1.StageId, WorkflowStageDecisionOutcome.Approved, "Reason", new[] { new string('A', 300) }, Actor, Now, ValidAuth));
            Assert.Equal(originalRevision, exec.Revision);
            Assert.Null(exec.Stages.First(s => s.Definition.StageId == s1.StageId).Decision);
        }

        [Fact]
        public void RecordDecision_AuthorityFailure_LeavesAggregateUnchanged()
        {
            var s1 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
            var s2 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>());
            var def = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, LcId, new WorkflowRuleSetReference("R", "1"), Now.AddDays(-1), Now, new[] { s1, s2 });
            var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, Now);
            
            exec.StartStage(s1.StageId, Actor, Now, ValidAuth);
            var originalRevision = exec.Revision;
            
            var invalidAuth = new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "WrongCap" }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5));
            Assert.Throws<MissingVerifiedAuthorityException>(() => exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), s1.StageId, WorkflowStageDecisionOutcome.Approved, null, null, Actor, Now, invalidAuth));
            Assert.Equal(originalRevision, exec.Revision);
            Assert.Null(exec.Stages.First(s => s.Definition.StageId == s1.StageId).Decision);
        }

        [Fact]
        public void RecordDecision_ConflictAfterAwaitingConsensus_LeavesAggregateUnchanged()
        {
            var s1 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
            var s2 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>());
            var def = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, LcId, new WorkflowRuleSetReference("R", "1"), Now.AddDays(-1), Now, new[] { s1, s2 });
            var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, Now);
            
            exec.StartStage(s1.StageId, Actor, Now, ValidAuth);
            var did = new WorkflowStageDecisionId(Guid.NewGuid());
            exec.RecordStageDecision(did, s1.StageId, WorkflowStageDecisionOutcome.Approved, null, null, Actor, Now, ValidAuth);
            
            var originalRevision = exec.Revision;
            Assert.Equal(WorkflowExecutionStatus.AwaitingConsensus, exec.Status);
            
            Assert.Throws<ConflictingWorkflowStageDecisionException>(() => exec.RecordStageDecision(did, s1.StageId, WorkflowStageDecisionOutcome.Approved, null, null, Actor, Now.AddMinutes(2), ValidAuth));
            Assert.Equal(originalRevision, exec.Revision);
            Assert.Equal(WorkflowExecutionStatus.AwaitingConsensus, exec.Status);
        }
    }
}













