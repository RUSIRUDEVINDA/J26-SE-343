using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaseInterestOfficerValuation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lease_interest_valuations",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseInterestRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    LandParcelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    FormType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AssessedLandValueLkr = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AssessedAreaHectares = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    InspectionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValuationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValuationReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IssuingOffice = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SiteCondition = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CultivationDetails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResidentialImprovements = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ProjectUseDetails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OtherTypeDetails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OfficerNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RecordedByOfficerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lease_interest_valuations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lease_interest_valuations_lease_interest_requests_LeaseInte~",
                        column: x => x.LeaseInterestRequestId,
                        principalSchema: "land_intelligence",
                        principalTable: "lease_interest_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lease_interest_valuations_LeaseInterestRequestId_Revision",
                schema: "land_intelligence",
                table: "lease_interest_valuations",
                columns: new[] { "LeaseInterestRequestId", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lease_interest_valuations",
                schema: "land_intelligence");
        }
    }
}
