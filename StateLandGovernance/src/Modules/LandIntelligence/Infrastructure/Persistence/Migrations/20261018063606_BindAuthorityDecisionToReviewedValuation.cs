using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BindAuthorityDecisionToReviewedValuation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedValuationId",
                schema: "land_intelligence",
                table: "lease_interest_authority_decisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedValuationRevision",
                schema: "land_intelligence",
                table: "lease_interest_authority_decisions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_lease_interest_authority_decisions_ReviewedValuationId",
                schema: "land_intelligence",
                table: "lease_interest_authority_decisions",
                column: "ReviewedValuationId");

            migrationBuilder.AddForeignKey(
                name: "FK_lease_interest_authority_decisions_lease_interest_valuation~",
                schema: "land_intelligence",
                table: "lease_interest_authority_decisions",
                column: "ReviewedValuationId",
                principalSchema: "land_intelligence",
                principalTable: "lease_interest_valuations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_lease_interest_authority_decisions_lease_interest_valuation~",
                schema: "land_intelligence",
                table: "lease_interest_authority_decisions");

            migrationBuilder.DropIndex(
                name: "IX_lease_interest_authority_decisions_ReviewedValuationId",
                schema: "land_intelligence",
                table: "lease_interest_authority_decisions");

            migrationBuilder.DropColumn(
                name: "ReviewedValuationId",
                schema: "land_intelligence",
                table: "lease_interest_authority_decisions");

            migrationBuilder.DropColumn(
                name: "ReviewedValuationRevision",
                schema: "land_intelligence",
                table: "lease_interest_authority_decisions");
        }
    }
}
