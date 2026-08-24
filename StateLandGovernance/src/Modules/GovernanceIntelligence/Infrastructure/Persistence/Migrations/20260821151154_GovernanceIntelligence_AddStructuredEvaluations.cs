using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GovernanceIntelligence_AddStructuredEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "compliance_evaluations",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EvaluationTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_evaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_evaluations_governance_audit_records_AuditRecord~",
                        column: x => x.AuditRecordId,
                        principalSchema: "governance_intelligence",
                        principalTable: "governance_audit_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "conditional_verification_evaluations",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerificationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalConditionsCount = table.Column<int>(type: "integer", nullable: false),
                    MandatoryConditionsCount = table.Column<int>(type: "integer", nullable: false),
                    SatisfiedMandatoryCount = table.Column<int>(type: "integer", nullable: false),
                    UnsatisfiedMandatoryCount = table.Column<int>(type: "integer", nullable: false),
                    SatisfiedOptionalCount = table.Column<int>(type: "integer", nullable: false),
                    PendingConditionsCount = table.Column<int>(type: "integer", nullable: false),
                    MissingConditionsCount = table.Column<int>(type: "integer", nullable: false),
                    ExpiredConditionsCount = table.Column<int>(type: "integer", nullable: false),
                    SummaryExplanation = table.Column<string>(type: "text", nullable: false),
                    RecommendedAction = table.Column<string>(type: "text", nullable: false),
                    EvaluationTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conditional_verification_evaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_conditional_verification_evaluations_governance_audit_recor~",
                        column: x => x.AuditRecordId,
                        principalSchema: "governance_intelligence",
                        principalTable: "governance_audit_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "conflict_evaluations",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalEvaluatedDecisions = table.Column<int>(type: "integer", nullable: false),
                    DetectedConflictsCount = table.Column<int>(type: "integer", nullable: false),
                    HighestSeverity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EvaluationTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conflict_evaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_conflict_evaluations_governance_audit_records_AuditRecordId",
                        column: x => x.AuditRecordId,
                        principalSchema: "governance_intelligence",
                        principalTable: "governance_audit_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "consensus_evaluations",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsensusEvaluationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalExpectedInstitutions = table.Column<int>(type: "integer", nullable: false),
                    SubmittedCount = table.Column<int>(type: "integer", nullable: false),
                    ParticipatingCount = table.Column<int>(type: "integer", nullable: false),
                    MissingCount = table.Column<int>(type: "integer", nullable: false),
                    ApprovalCount = table.Column<int>(type: "integer", nullable: false),
                    RejectionCount = table.Column<int>(type: "integer", nullable: false),
                    ConditionalApprovalCount = table.Column<int>(type: "integer", nullable: false),
                    AbstentionCount = table.Column<int>(type: "integer", nullable: false),
                    PendingCount = table.Column<int>(type: "integer", nullable: false),
                    QuorumSatisfied = table.Column<bool>(type: "boolean", nullable: false),
                    MandatoryInstitutionsSatisfied = table.Column<bool>(type: "boolean", nullable: false),
                    ConsensusThresholdSatisfied = table.Column<bool>(type: "boolean", nullable: false),
                    BlockingInstitutionCount = table.Column<int>(type: "integer", nullable: false),
                    SummaryExplanation = table.Column<string>(type: "text", nullable: false),
                    RecommendedAction = table.Column<string>(type: "text", nullable: false),
                    EvaluationTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consensus_evaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consensus_evaluations_governance_audit_records_AuditRecordId",
                        column: x => x.AuditRecordId,
                        principalSchema: "governance_intelligence",
                        principalTable: "governance_audit_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "governance_explanations",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExplanationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OverallSeverity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RequiresHumanReview = table.Column<bool>(type: "boolean", nullable: false),
                    Disclaimer = table.Column<string>(type: "text", nullable: false),
                    EvaluationTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_governance_explanations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_governance_explanations_governance_audit_records_AuditRecor~",
                        column: x => x.AuditRecordId,
                        principalSchema: "governance_intelligence",
                        principalTable: "governance_audit_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "risk_evaluations",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OverallRiskScore = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RequiresHumanReview = table.Column<bool>(type: "boolean", nullable: false),
                    EvaluationTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_evaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_risk_evaluations_governance_audit_records_AuditRecordId",
                        column: x => x.AuditRecordId,
                        principalSchema: "governance_intelligence",
                        principalTable: "governance_audit_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "compliance_conditions",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComplianceEvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    RequiredByDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_conditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_conditions_compliance_evaluations_ComplianceEval~",
                        column: x => x.ComplianceEvaluationId,
                        principalSchema: "governance_intelligence",
                        principalTable: "compliance_evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "compliance_violations",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComplianceEvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_violations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_violations_compliance_evaluations_ComplianceEval~",
                        column: x => x.ComplianceEvaluationId,
                        principalSchema: "governance_intelligence",
                        principalTable: "compliance_evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "condition_results",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConditionalVerificationEvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConditionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FailureReason = table.Column<string>(type: "text", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_condition_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_condition_results_conditional_verification_evaluations_Cond~",
                        column: x => x.ConditionalVerificationEvaluationId,
                        principalSchema: "governance_intelligence",
                        principalTable: "conditional_verification_evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "conflict_findings",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConflictEvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConflictId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ConflictType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DetectionStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InvolvedDecisionIdsJson = table.Column<string>(type: "text", nullable: false),
                    InvolvedInstitutionsJson = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: false),
                    EvidenceRule = table.Column<string>(type: "text", nullable: false),
                    RecommendedAction = table.Column<string>(type: "text", nullable: false),
                    DetectionTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conflict_findings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_conflict_findings_conflict_evaluations_ConflictEvaluationId",
                        column: x => x.ConflictEvaluationId,
                        principalSchema: "governance_intelligence",
                        principalTable: "conflict_evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "institution_positions",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsensusEvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Position = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AuthorityRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SubmittedTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institution_positions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_institution_positions_consensus_evaluations_ConsensusEvalua~",
                        column: x => x.ConsensusEvaluationId,
                        principalSchema: "governance_intelligence",
                        principalTable: "consensus_evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "governance_explanation_items",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GovernanceExplanationEvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceEngine = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OutcomeStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PlainLanguageExplanation = table.Column<string>(type: "text", nullable: false),
                    RecommendedAction = table.Column<string>(type: "text", nullable: false),
                    RequiresHumanAttention = table.Column<bool>(type: "boolean", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_governance_explanation_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_governance_explanation_items_governance_explanations_Govern~",
                        column: x => x.GovernanceExplanationEvaluationId,
                        principalSchema: "governance_intelligence",
                        principalTable: "governance_explanations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "risk_indicators",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RiskEvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IndicatorId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ScoreContribution = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InvolvedActorsJson = table.Column<string>(type: "text", nullable: false),
                    TriggeredRule = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: false),
                    RecommendedAction = table.Column<string>(type: "text", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_indicators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_risk_indicators_risk_evaluations_RiskEvaluationId",
                        column: x => x.RiskEvaluationId,
                        principalSchema: "governance_intelligence",
                        principalTable: "risk_evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_conditions_ComplianceEvaluationId",
                schema: "governance_intelligence",
                table: "compliance_conditions",
                column: "ComplianceEvaluationId");

            migrationBuilder.CreateIndex(
                name: "idx_comp_eval_timestamp",
                schema: "governance_intelligence",
                table: "compliance_evaluations",
                column: "EvaluationTimestamp");

            migrationBuilder.CreateIndex(
                name: "idx_uniq_compliance_eval_audit_id",
                schema: "governance_intelligence",
                table: "compliance_evaluations",
                column: "AuditRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_compliance_violations_ComplianceEvaluationId",
                schema: "governance_intelligence",
                table: "compliance_violations",
                column: "ComplianceEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_condition_results_ConditionalVerificationEvaluationId",
                schema: "governance_intelligence",
                table: "condition_results",
                column: "ConditionalVerificationEvaluationId");

            migrationBuilder.CreateIndex(
                name: "idx_cond_verif_subject_id",
                schema: "governance_intelligence",
                table: "conditional_verification_evaluations",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "idx_cond_verif_timestamp",
                schema: "governance_intelligence",
                table: "conditional_verification_evaluations",
                column: "EvaluationTimestamp");

            migrationBuilder.CreateIndex(
                name: "idx_uniq_cond_verif_eval_audit_id",
                schema: "governance_intelligence",
                table: "conditional_verification_evaluations",
                column: "AuditRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_conf_eval_timestamp",
                schema: "governance_intelligence",
                table: "conflict_evaluations",
                column: "EvaluationTimestamp");

            migrationBuilder.CreateIndex(
                name: "idx_uniq_conflict_eval_audit_id",
                schema: "governance_intelligence",
                table: "conflict_evaluations",
                column: "AuditRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_conf_finding_subject_id",
                schema: "governance_intelligence",
                table: "conflict_findings",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_conflict_findings_ConflictEvaluationId",
                schema: "governance_intelligence",
                table: "conflict_findings",
                column: "ConflictEvaluationId");

            migrationBuilder.CreateIndex(
                name: "idx_cons_eval_subject_id",
                schema: "governance_intelligence",
                table: "consensus_evaluations",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "idx_cons_eval_timestamp",
                schema: "governance_intelligence",
                table: "consensus_evaluations",
                column: "EvaluationTimestamp");

            migrationBuilder.CreateIndex(
                name: "idx_uniq_consensus_eval_audit_id",
                schema: "governance_intelligence",
                table: "consensus_evaluations",
                column: "AuditRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_governance_explanation_items_GovernanceExplanationEvaluatio~",
                schema: "governance_intelligence",
                table: "governance_explanation_items",
                column: "GovernanceExplanationEvaluationId");

            migrationBuilder.CreateIndex(
                name: "idx_gov_expl_subject_id",
                schema: "governance_intelligence",
                table: "governance_explanations",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "idx_gov_expl_timestamp",
                schema: "governance_intelligence",
                table: "governance_explanations",
                column: "EvaluationTimestamp");

            migrationBuilder.CreateIndex(
                name: "idx_uniq_gov_expl_eval_audit_id",
                schema: "governance_intelligence",
                table: "governance_explanations",
                column: "AuditRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_inst_pos_institution_id",
                schema: "governance_intelligence",
                table: "institution_positions",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_institution_positions_ConsensusEvaluationId",
                schema: "governance_intelligence",
                table: "institution_positions",
                column: "ConsensusEvaluationId");

            migrationBuilder.CreateIndex(
                name: "idx_risk_eval_subject_id",
                schema: "governance_intelligence",
                table: "risk_evaluations",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "idx_risk_eval_timestamp",
                schema: "governance_intelligence",
                table: "risk_evaluations",
                column: "EvaluationTimestamp");

            migrationBuilder.CreateIndex(
                name: "idx_uniq_risk_eval_audit_id",
                schema: "governance_intelligence",
                table: "risk_evaluations",
                column: "AuditRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_risk_ind_subject_id",
                schema: "governance_intelligence",
                table: "risk_indicators",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_risk_indicators_RiskEvaluationId",
                schema: "governance_intelligence",
                table: "risk_indicators",
                column: "RiskEvaluationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compliance_conditions",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "compliance_violations",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "condition_results",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "conflict_findings",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "governance_explanation_items",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "institution_positions",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "risk_indicators",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "compliance_evaluations",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "conditional_verification_evaluations",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "conflict_evaluations",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "governance_explanations",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "consensus_evaluations",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "risk_evaluations",
                schema: "governance_intelligence");
        }
    }
}
