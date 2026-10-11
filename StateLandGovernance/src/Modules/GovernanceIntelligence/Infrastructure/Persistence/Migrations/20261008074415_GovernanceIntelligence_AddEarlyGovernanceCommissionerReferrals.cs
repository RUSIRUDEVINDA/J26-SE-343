using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GovernanceIntelligence_AddEarlyGovernanceCommissionerReferrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "workflow_run_id",
                schema: "governance_intelligence",
                table: "early_governance_screening_evaluations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "early_governance_referrals",
                schema: "governance_intelligence",
                columns: table => new
                {
                    referral_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    workflow_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    evidence_references_json = table.Column<string>(type: "jsonb", nullable: false),
                    requested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    delivery_state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    delivery_attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    acknowledged_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    commissioner_review_process_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_early_governance_referrals", x => x.referral_id);
                    table.CheckConstraint("CK_early_governance_referrals_delivery_attempt_count", "delivery_attempt_count >= 0");
                    table.CheckConstraint("CK_early_governance_referrals_delivery_state", "delivery_state IN ('Pending', 'Delivering', 'DeliveryFailed', 'Acknowledged')");
                    table.ForeignKey(
                        name: "FK_early_governance_referrals_early_governance_screening_evalu~",
                        column: x => x.assessment_id,
                        principalSchema: "governance_intelligence",
                        principalTable: "early_governance_screening_evaluations",
                        principalColumn: "assessment_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_early_governance_screening_evaluations_case_id_created_at_u~",
                schema: "governance_intelligence",
                table: "early_governance_screening_evaluations",
                columns: new[] { "case_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_early_governance_referrals_assessment_id",
                schema: "governance_intelligence",
                table: "early_governance_referrals",
                column: "assessment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_early_governance_referrals_case_id_requested_at_utc",
                schema: "governance_intelligence",
                table: "early_governance_referrals",
                columns: new[] { "case_id", "requested_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_early_governance_referrals_correlation_id",
                schema: "governance_intelligence",
                table: "early_governance_referrals",
                column: "correlation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_early_governance_referrals_delivery_state",
                schema: "governance_intelligence",
                table: "early_governance_referrals",
                column: "delivery_state");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "early_governance_referrals",
                schema: "governance_intelligence");

            migrationBuilder.DropIndex(
                name: "IX_early_governance_screening_evaluations_case_id_created_at_u~",
                schema: "governance_intelligence",
                table: "early_governance_screening_evaluations");

            migrationBuilder.DropColumn(
                name: "workflow_run_id",
                schema: "governance_intelligence",
                table: "early_governance_screening_evaluations");
        }
    }
}
