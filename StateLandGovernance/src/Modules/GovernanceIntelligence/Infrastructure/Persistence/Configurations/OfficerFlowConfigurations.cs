using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;
public sealed class OfficerInvestigationConfiguration:IEntityTypeConfiguration<OfficerInvestigationEntity>{
 public void Configure(EntityTypeBuilder<OfficerInvestigationEntity>b){
 b.ToTable("officer_investigations",t=>{t.HasCheckConstraint("ck_officer_findings", "jsonb_typeof(\"FindingsJson\")='array' AND jsonb_array_length(\"FindingsJson\")=7");t.HasCheckConstraint("ck_officer_input_hash","length(\"InputSha256\")=64");});b.HasKey(x=>x.Id);b.HasAlternateKey(x=>new{x.Id,x.CreatorActorId});b.HasIndex(x=>new{x.CreatorActorId,x.SubmissionKey}).IsUnique();
 b.Property(x=>x.ProposalId).HasMaxLength(120);b.Property(x=>x.SourceVersion).HasMaxLength(100);b.Property(x=>x.InputSha256).HasMaxLength(64);b.Property(x=>x.FindingsJson).HasColumnType("jsonb");
 b.HasOne<OfficerInvestigationEntity>().WithMany().HasForeignKey(x=>new{x.SupersedesId,x.CreatorActorId}).HasPrincipalKey(x=>new{x.Id,x.CreatorActorId}).OnDelete(DeleteBehavior.Restrict);
 }}
public sealed class OfficerAssessmentRequestConfiguration:IEntityTypeConfiguration<OfficerAssessmentRequestEntity>{
 public void Configure(EntityTypeBuilder<OfficerAssessmentRequestEntity>b){
 b.ToTable("officer_assessment_requests",t=>{
 t.HasCheckConstraint("ck_officer_kind","\"Kind\" IN ('early','anomaly')");t.HasCheckConstraint("ck_officer_state","\"State\" IN ('Pending','Saved','Failed','Unknown')");t.HasCheckConstraint("ck_officer_request_hash","length(\"InputSha256\")=64");t.HasCheckConstraint("ck_officer_revision","\"Revision\">0");
 t.HasCheckConstraint("ck_officer_request_source","(\"Kind\"='early' AND \"InvestigationId\" IS NOT NULL AND \"Scenario\" IS NULL AND \"WorkflowRunId\" IS NULL AND \"ProjectionId\" IS NULL) OR (\"Kind\"='anomaly' AND \"InvestigationId\" IS NULL AND \"Scenario\" IS NOT NULL AND \"WorkflowRunId\" IS NOT NULL AND \"ProjectionId\" IS NOT NULL)");
 t.HasCheckConstraint("ck_officer_result","(\"State\"='Saved' AND \"FinishedAtUtc\" IS NOT NULL AND ((\"Kind\"='early' AND \"EarlyAssessmentId\" IS NOT NULL AND \"AnomalyAssessmentId\" IS NULL) OR (\"Kind\"='anomaly' AND \"AnomalyAssessmentId\" IS NOT NULL AND \"EarlyAssessmentId\" IS NULL))) OR (\"State\"<>'Saved' AND \"EarlyAssessmentId\" IS NULL AND \"AnomalyAssessmentId\" IS NULL)");});
 b.HasKey(x=>x.Id);b.HasAlternateKey(x=>new{x.Id,x.CreatorActorId});b.HasIndex(x=>new{x.CreatorActorId,x.SubmissionKey}).IsUnique();b.HasIndex(x=>x.WorkflowRunId).IsUnique();b.HasIndex(x=>new{x.CreatorActorId,x.Kind,x.ScopeKey,x.SourceVersion}).IsUnique().HasDatabaseName("ux_officer_assessment_active_scope").HasFilter("\"State\" IN ('Pending','Unknown')");b.HasIndex(x=>new{x.CreatorActorId,x.CreatedAtUtc}).HasDatabaseName("ix_officer_assessment_history");
 b.Property(x=>x.Kind).HasMaxLength(16);b.Property(x=>x.ScopeKey).HasMaxLength(120);b.Property(x=>x.SourceVersion).HasMaxLength(100);b.Property(x=>x.InputSha256).HasMaxLength(64);b.Property(x=>x.Scenario).HasMaxLength(64);b.Property(x=>x.State).HasMaxLength(16);b.Property(x=>x.FailureCode).HasMaxLength(100);b.Property(x=>x.Revision).IsConcurrencyToken();
 b.HasOne<OfficerInvestigationEntity>().WithMany().HasForeignKey(x=>new{x.InvestigationId,x.CreatorActorId}).HasPrincipalKey(x=>new{x.Id,x.CreatorActorId}).OnDelete(DeleteBehavior.Restrict);
 b.HasOne<Component3WorkflowProjectionEntity>().WithMany().HasForeignKey(x=>x.ProjectionId).OnDelete(DeleteBehavior.Restrict);
 b.HasOne<EarlyGovernanceScreeningEvaluationEntity>().WithMany().HasForeignKey(x=>x.EarlyAssessmentId).OnDelete(DeleteBehavior.Restrict);
 b.HasOne<WorkflowAnomalyAssessmentEntity>().WithMany().HasForeignKey(x=>x.AnomalyAssessmentId).OnDelete(DeleteBehavior.Restrict);
 }}
public sealed class OfficerActionEventConfiguration:IEntityTypeConfiguration<OfficerActionEventEntity>{
 public void Configure(EntityTypeBuilder<OfficerActionEventEntity>b){b.ToTable("officer_action_events",t=>{
 t.HasCheckConstraint("ck_officer_event_parent","(\"InvestigationId\" IS NULL) <> (\"RequestId\" IS NULL)");t.HasCheckConstraint("ck_officer_event_hash","length(\"EventSha256\")=64");t.HasCheckConstraint("ck_officer_operation","\"Operation\" IN ('InvestigationSaved','RequestSubmitted','InferenceStarted','AssessmentSaved','AssessmentFailed','OutcomeUnknown') OR (\"Operation\"='InvestigationFinalized' AND \"InvestigationId\" IS NOT NULL AND \"RequestId\" IS NULL)");});
 b.HasIndex(x=>x.InvestigationId).IsUnique().HasDatabaseName("ux_officer_investigation_finalization").HasFilter("\"Operation\" = 'InvestigationFinalized'");
 b.HasKey(x=>x.Id);b.Property(x=>x.Operation).HasMaxLength(40);b.Property(x=>x.DetailsJson).HasColumnType("jsonb");b.Property(x=>x.EventSha256).HasMaxLength(64);b.HasIndex(x=>new{x.RequestId,x.CreatedAtUtc}).HasDatabaseName("ix_officer_action_events_request");b.HasIndex(x=>new{x.RequestId,x.Operation}).IsUnique().HasDatabaseName("ux_officer_action_request_operation").HasFilter("\"RequestId\" IS NOT NULL");
 b.HasOne<OfficerInvestigationEntity>().WithMany().HasForeignKey(x=>new{x.InvestigationId,x.CreatorActorId}).HasPrincipalKey(x=>new{x.Id,x.CreatorActorId}).OnDelete(DeleteBehavior.Restrict);
 b.HasOne<OfficerAssessmentRequestEntity>().WithMany().HasForeignKey(x=>new{x.RequestId,x.CreatorActorId}).HasPrincipalKey(x=>new{x.Id,x.CreatorActorId}).OnDelete(DeleteBehavior.Restrict);
 }}
