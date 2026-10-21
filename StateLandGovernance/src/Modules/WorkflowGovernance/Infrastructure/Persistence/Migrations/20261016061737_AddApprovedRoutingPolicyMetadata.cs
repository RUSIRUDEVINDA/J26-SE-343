using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovedRoutingPolicyMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "policy_json",
                schema: "workflow_governance",
                table: "institutional_routing_configs",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.NotSupportedException("Forward-only migration: approved routing metadata must be preserved.");
        }
    }
}
