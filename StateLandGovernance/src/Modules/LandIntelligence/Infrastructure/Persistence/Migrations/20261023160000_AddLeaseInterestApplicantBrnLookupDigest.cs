using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LandIntelligenceDbContext))]
[Migration("20261023160000_AddLeaseInterestApplicantBrnLookupDigest")]
public partial class AddLeaseInterestApplicantBrnLookupDigest : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ApplicantBrnLookupDigest",
            schema: "land_intelligence",
            table: "lease_interest_requests",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_lease_interest_requests_ApplicantBrnLookupDigest",
            schema: "land_intelligence",
            table: "lease_interest_requests",
            column: "ApplicantBrnLookupDigest");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_lease_interest_requests_ApplicantBrnLookupDigest",
            schema: "land_intelligence",
            table: "lease_interest_requests");

        migrationBuilder.DropColumn(
            name: "ApplicantBrnLookupDigest",
            schema: "land_intelligence",
            table: "lease_interest_requests");
    }
}
