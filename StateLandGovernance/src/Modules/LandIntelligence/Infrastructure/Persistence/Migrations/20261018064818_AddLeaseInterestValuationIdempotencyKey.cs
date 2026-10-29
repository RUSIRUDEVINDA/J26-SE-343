using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaseInterestValuationIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "land_intelligence",
                table: "lease_interest_valuations",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            // Backfill unique keys for any rows created before idempotent saves existed.
            migrationBuilder.Sql(
                """
                UPDATE land_intelligence.lease_interest_valuations
                SET "IdempotencyKey" = 'legacy-' || "Id"::text
                WHERE "IdempotencyKey" IS NULL OR btrim("IdempotencyKey") = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                schema: "land_intelligence",
                table: "lease_interest_valuations",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_lease_interest_valuations_LeaseInterestRequestId_Idempotenc~",
                schema: "land_intelligence",
                table: "lease_interest_valuations",
                columns: new[] { "LeaseInterestRequestId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lease_interest_valuations_LeaseInterestRequestId_Idempotenc~",
                schema: "land_intelligence",
                table: "lease_interest_valuations");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "land_intelligence",
                table: "lease_interest_valuations");
        }
    }
}
