using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnedRegulatoryProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regulatory_evaluation_provenance",
                schema: "governance_intelligence",
                columns: table => new
                {
                    EvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProvenanceJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulatory_evaluation_provenance", x => x.EvaluationId);
                    table.ForeignKey(
                        name: "FK_regulatory_evaluation_provenance_compliance_evaluations_Eva~",
                        column: x => x.EvaluationId,
                        principalSchema: "governance_intelligence",
                        principalTable: "compliance_evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_evaluation_provenance_CreatorActorId_SubmissionK~",
                schema: "governance_intelligence",
                table: "regulatory_evaluation_provenance",
                columns: new[] { "CreatorActorId", "SubmissionKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Preserve regulatory ownership and provenance history; disable writes instead.");
        }
    }
}
