using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPostWorkflowDemonstrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "post_workflow_demonstrations",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scenario = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Failure = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    AttemptsJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_workflow_demonstrations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_post_workflow_demonstrations_CreatorActorId_Scenario_Version",
                schema: "governance_intelligence",
                table: "post_workflow_demonstrations",
                columns: new[] { "CreatorActorId", "Scenario", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_post_workflow_demonstrations_WorkflowRunId",
                schema: "governance_intelligence",
                table: "post_workflow_demonstrations",
                column: "WorkflowRunId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Preserve ownership and assessment history; disable writes and revoke permissions instead.");
        }
    }
}
