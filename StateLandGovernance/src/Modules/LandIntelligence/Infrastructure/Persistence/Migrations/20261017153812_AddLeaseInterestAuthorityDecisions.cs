using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaseInterestAuthorityDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lease_interest_authority_decisions",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseInterestRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ResultingStatus = table.Column<int>(type: "integer", nullable: false),
                    RecordedByAuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lease_interest_authority_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lease_interest_authority_decisions_lease_interest_requests_~",
                        column: x => x.LeaseInterestRequestId,
                        principalSchema: "land_intelligence",
                        principalTable: "lease_interest_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lease_interest_authority_decisions_LeaseInterestRequestId",
                schema: "land_intelligence",
                table: "lease_interest_authority_decisions",
                column: "LeaseInterestRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_lease_interest_authority_decisions_LeaseInterestRequestId_R~",
                schema: "land_intelligence",
                table: "lease_interest_authority_decisions",
                columns: new[] { "LeaseInterestRequestId", "RecordedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lease_interest_authority_decisions",
                schema: "land_intelligence");
        }
    }
}
