using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaseInterestApplicantNic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicantNicCiphertext",
                schema: "land_intelligence",
                table: "lease_interest_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantNicLookupDigest",
                schema: "land_intelligence",
                table: "lease_interest_requests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_lease_interest_requests_ApplicantNicLookupDigest",
                schema: "land_intelligence",
                table: "lease_interest_requests",
                column: "ApplicantNicLookupDigest");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lease_interest_requests_ApplicantNicLookupDigest",
                schema: "land_intelligence",
                table: "lease_interest_requests");

            migrationBuilder.DropColumn(
                name: "ApplicantNicCiphertext",
                schema: "land_intelligence",
                table: "lease_interest_requests");

            migrationBuilder.DropColumn(
                name: "ApplicantNicLookupDigest",
                schema: "land_intelligence",
                table: "lease_interest_requests");
        }
    }
}
