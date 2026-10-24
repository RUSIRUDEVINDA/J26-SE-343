using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableIntakeAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "case_assignments",
                schema: "workflow_governance",
                columns: table => new
                {
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    StateJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_assignments", x => x.CaseId);
                    table.ForeignKey(
                        name: "FK_case_assignments_lease_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "workflow_governance",
                        principalTable: "lease_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "intake_jurisdiction_policies",
                schema: "workflow_governance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Identifier = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PolicyJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intake_jurisdiction_policies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_intake_jurisdiction_policies_Identifier_Version",
                schema: "workflow_governance",
                table: "intake_jurisdiction_policies",
                columns: new[] { "Identifier", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Assignment history and routing definitions must be preserved. Use a reviewed forward repair; destructive rollback is not supported.");
        }
    }
}
