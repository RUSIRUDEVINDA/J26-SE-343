using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaseInterestStatusHistoryAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActorId",
                schema: "land_intelligence",
                table: "lease_interest_status_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActorKind",
                schema: "land_intelligence",
                table: "lease_interest_status_history",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "VisibleToApplicant",
                schema: "land_intelligence",
                table: "lease_interest_status_history",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActorId",
                schema: "land_intelligence",
                table: "lease_interest_status_history");

            migrationBuilder.DropColumn(
                name: "ActorKind",
                schema: "land_intelligence",
                table: "lease_interest_status_history");

            migrationBuilder.DropColumn(
                name: "VisibleToApplicant",
                schema: "land_intelligence",
                table: "lease_interest_status_history");
        }
    }
}
