using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(LandIntelligenceDbContext))]
    [Migration("20261024133000_AddLeaseInterestOneApprovalPerParcel")]
    public partial class AddLeaseInterestOneApprovalPerParcel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                  IF EXISTS (
                    SELECT 1
                    FROM land_intelligence.lease_interest_requests
                    WHERE "Status" = 7
                    GROUP BY "LandParcelId"
                    HAVING COUNT(*) > 1
                  ) THEN
                    RAISE EXCEPTION
                      'Cannot enforce one approved-in-principle request per parcel: multiple rows with Status=7 exist for the same LandParcelId. Resolve duplicates manually; historical decisions are not rewritten automatically.';
                  END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_lease_interest_requests_one_approved_in_principle_per_parcel",
                schema: "land_intelligence",
                table: "lease_interest_requests",
                column: "LandParcelId",
                unique: true,
                filter: "\"Status\" = 7");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lease_interest_requests_one_approved_in_principle_per_parcel",
                schema: "land_intelligence",
                table: "lease_interest_requests");
        }
    }
}
