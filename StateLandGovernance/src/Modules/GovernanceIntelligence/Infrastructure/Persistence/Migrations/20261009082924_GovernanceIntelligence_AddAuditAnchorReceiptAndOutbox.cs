using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GovernanceIntelligence_AddAuditAnchorReceiptAndOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "governance_audit_anchor_outbox",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EngineType = table.Column<int>(type: "integer", nullable: false),
                    RecordVersion = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_governance_audit_anchor_outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "governance_audit_anchor_receipts",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AnchorStatus = table.Column<int>(type: "integer", nullable: false),
                    TransactionReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    BlockNumber = table.Column<long>(type: "bigint", nullable: true),
                    ContractAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AnchoredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastVerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerificationStatus = table.Column<int>(type: "integer", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_governance_audit_anchor_receipts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_gov_anchor_outbox_audit_record_id",
                schema: "governance_intelligence",
                table: "governance_audit_anchor_outbox",
                column: "AuditRecordId");

            migrationBuilder.CreateIndex(
                name: "idx_gov_anchor_outbox_status_next_attempt",
                schema: "governance_intelligence",
                table: "governance_audit_anchor_outbox",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "idx_gov_anchor_receipt_audit_record_id",
                schema: "governance_intelligence",
                table: "governance_audit_anchor_receipts",
                column: "AuditRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_gov_anchor_receipt_record_hash",
                schema: "governance_intelligence",
                table: "governance_audit_anchor_receipts",
                column: "RecordHash");

            migrationBuilder.CreateIndex(
                name: "idx_gov_anchor_receipt_status",
                schema: "governance_intelligence",
                table: "governance_audit_anchor_receipts",
                column: "AnchorStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "governance_audit_anchor_outbox",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "governance_audit_anchor_receipts",
                schema: "governance_intelligence");
        }
    }
}
