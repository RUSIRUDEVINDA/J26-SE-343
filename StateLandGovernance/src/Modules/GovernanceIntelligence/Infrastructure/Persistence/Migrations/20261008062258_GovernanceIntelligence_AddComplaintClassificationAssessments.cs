using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GovernanceIntelligence_AddComplaintClassificationAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "complaint_classification_assessments",
                schema: "governance_intelligence",
                columns: table => new
                {
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    complaint_text = table.Column<string>(type: "text", nullable: false),
                    model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    predicted_category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    administrative_procedural_integrity_probability = table.Column<decimal>(type: "numeric(18,17)", precision: 18, scale: 17, nullable: false),
                    lease_revenue_payment_enforcement_probability = table.Column<decimal>(type: "numeric(18,17)", precision: 18, scale: 17, nullable: false),
                    unauthorized_allocation_transfer_use_probability = table.Column<decimal>(type: "numeric(18,17)", precision: 18, scale: 17, nullable: false),
                    protected_environmental_lease_misuse_probability = table.Column<decimal>(type: "numeric(18,17)", precision: 18, scale: 17, nullable: false),
                    advisory_note = table.Column<string>(type: "text", nullable: false),
                    closed_set_note = table.Column<string>(type: "text", nullable: false),
                    assessed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_complaint_classification_assessments", x => x.assessment_id);
                    table.CheckConstraint("CK_complaint_classification_assessments_probability_range", "administrative_procedural_integrity_probability >= 0 AND administrative_procedural_integrity_probability <= 1 AND lease_revenue_payment_enforcement_probability >= 0 AND lease_revenue_payment_enforcement_probability <= 1 AND unauthorized_allocation_transfer_use_probability >= 0 AND unauthorized_allocation_transfer_use_probability <= 1 AND protected_environmental_lease_misuse_probability >= 0 AND protected_environmental_lease_misuse_probability <= 1");
                    table.CheckConstraint("CK_complaint_classification_assessments_probability_sum", "abs(administrative_procedural_integrity_probability + lease_revenue_payment_enforcement_probability + unauthorized_allocation_transfer_use_probability + protected_environmental_lease_misuse_probability - 1.0) <= 0.000001");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "complaint_classification_assessments",
                schema: "governance_intelligence");
        }
    }
}
