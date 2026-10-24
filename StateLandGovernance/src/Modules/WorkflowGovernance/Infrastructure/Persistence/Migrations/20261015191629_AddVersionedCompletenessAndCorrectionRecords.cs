using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionedCompletenessAndCorrectionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "workflow_governance",
                table: "lease_cases",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "completeness_definitions",
                schema: "workflow_governance",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Identifier = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceReference = table.Column<string>(type: "text", nullable: false),
                    DefinitionJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ApprovedByActorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_completeness_definitions", x => new { x.Kind, x.Identifier, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "correction_records",
                schema: "workflow_governance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    RecordJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_correction_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_correction_records_lease_cases_LeaseCaseId",
                        column: x => x.LeaseCaseId,
                        principalSchema: "workflow_governance",
                        principalTable: "lease_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_correction_records_LeaseCaseId",
                schema: "workflow_governance",
                table: "correction_records",
                column: "LeaseCaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "completeness_definitions",
                schema: "workflow_governance");

            migrationBuilder.DropTable(
                name: "correction_records",
                schema: "workflow_governance");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "workflow_governance",
                table: "lease_cases");
        }
    }
}
