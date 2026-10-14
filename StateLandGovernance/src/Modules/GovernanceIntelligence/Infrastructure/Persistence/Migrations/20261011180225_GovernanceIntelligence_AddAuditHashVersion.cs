using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GovernanceIntelligence_AddAuditHashVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows retain their original payload semantics; no digest, receipt, or transaction is rewritten.
            migrationBuilder.AddColumn<string>(
                name: "HashVersion",
                schema: "governance_intelligence",
                table: "governance_audit_records",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "AUDIT-V1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_gov_audit_records_hash_version",
                schema: "governance_intelligence",
                table: "governance_audit_records",
                sql: "\"HashVersion\" IN ('AUDIT-V1', 'AUDIT-V2')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_gov_audit_records_hash_version",
                schema: "governance_intelligence",
                table: "governance_audit_records");

            migrationBuilder.DropColumn(
                name: "HashVersion",
                schema: "governance_intelligence",
                table: "governance_audit_records");
        }
    }
}
