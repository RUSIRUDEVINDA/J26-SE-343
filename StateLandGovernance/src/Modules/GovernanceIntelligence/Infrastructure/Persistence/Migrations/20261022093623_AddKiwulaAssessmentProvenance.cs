using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKiwulaAssessmentProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "kiwula_assessment_provenance",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Engine = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ConflictEvaluationId = table.Column<Guid>(type: "uuid", nullable: true),
                    EarlyAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kiwula_assessment_provenance", x => x.Id);
                    table.CheckConstraint("CK_kiwula_engine_link", "(\"Engine\" = 'conflict' AND \"ConflictEvaluationId\" IS NOT NULL AND \"EarlyAssessmentId\" IS NULL) OR (\"Engine\" = 'early' AND \"EarlyAssessmentId\" IS NOT NULL AND \"ConflictEvaluationId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_kiwula_assessment_provenance_conflict_evaluations_ConflictE~",
                        column: x => x.ConflictEvaluationId,
                        principalSchema: "governance_intelligence",
                        principalTable: "conflict_evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_kiwula_assessment_provenance_early_governance_screening_eva~",
                        column: x => x.EarlyAssessmentId,
                        principalSchema: "governance_intelligence",
                        principalTable: "early_governance_screening_evaluations",
                        principalColumn: "assessment_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_kiwula_assessment_provenance_ConflictEvaluationId",
                schema: "governance_intelligence",
                table: "kiwula_assessment_provenance",
                column: "ConflictEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_kiwula_assessment_provenance_CreatorActorId_SubmissionKey",
                schema: "governance_intelligence",
                table: "kiwula_assessment_provenance",
                columns: new[] { "CreatorActorId", "SubmissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kiwula_assessment_provenance_EarlyAssessmentId",
                schema: "governance_intelligence",
                table: "kiwula_assessment_provenance",
                column: "EarlyAssessmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Preserve assessment ownership/history. Disable writes and revoke new grants; restore into an isolated recovery database when needed.");
        }
    }
}
