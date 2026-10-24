using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(LandIntelligenceDbContext))]
    [Migration("20261024160000_AddLeaseInterestTwoOfficerHandoff")]
    public partial class AddLeaseInterestTwoOfficerHandoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VerificationCompletedByOfficerId",
                schema: "land_intelligence",
                table: "lease_interest_requests",
                type: "uuid",
                nullable: true);

            // Legacy rows referred before valuation existed: move to awaiting valuation when no worksheet yet.
            migrationBuilder.Sql(
                """
                UPDATE land_intelligence.lease_interest_requests AS r
                SET "Status" = 9
                WHERE r."Status" = 6
                  AND NOT EXISTS (
                    SELECT 1
                    FROM land_intelligence.lease_interest_valuations AS v
                    WHERE v."LeaseInterestRequestId" = r."Id"
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerificationCompletedByOfficerId",
                schema: "land_intelligence",
                table: "lease_interest_requests");
        }
    }
}
