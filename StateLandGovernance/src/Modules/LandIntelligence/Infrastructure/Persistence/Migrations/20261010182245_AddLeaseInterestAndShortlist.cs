using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaseInterestAndShortlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lease_interest_requests",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LandParcelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AcknowledgementReference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SubmitIdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    RequestedPurpose = table.Column<int>(type: "integer", nullable: false),
                    RequestedDurationYears = table.Column<int>(type: "integer", nullable: true),
                    ConsentToPreliminaryAssessment = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lease_interest_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "parcel_shortlist_items",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LandParcelId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcel_shortlist_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lease_interest_status_history",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseInterestRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lease_interest_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lease_interest_status_history_lease_interest_requests_Lease~",
                        column: x => x.LeaseInterestRequestId,
                        principalSchema: "land_intelligence",
                        principalTable: "lease_interest_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lease_interest_requests_AcknowledgementReference",
                schema: "land_intelligence",
                table: "lease_interest_requests",
                column: "AcknowledgementReference",
                unique: true,
                filter: "\"AcknowledgementReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_lease_interest_requests_LandParcelId",
                schema: "land_intelligence",
                table: "lease_interest_requests",
                column: "LandParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_lease_interest_requests_OwnerId",
                schema: "land_intelligence",
                table: "lease_interest_requests",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_lease_interest_requests_OwnerId_SubmitIdempotencyKey",
                schema: "land_intelligence",
                table: "lease_interest_requests",
                columns: new[] { "OwnerId", "SubmitIdempotencyKey" },
                unique: true,
                filter: "\"SubmitIdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_lease_interest_status_history_LeaseInterestRequestId",
                schema: "land_intelligence",
                table: "lease_interest_status_history",
                column: "LeaseInterestRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_parcel_shortlist_items_OwnerId",
                schema: "land_intelligence",
                table: "parcel_shortlist_items",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_parcel_shortlist_items_OwnerId_LandParcelId",
                schema: "land_intelligence",
                table: "parcel_shortlist_items",
                columns: new[] { "OwnerId", "LandParcelId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lease_interest_status_history",
                schema: "land_intelligence");

            migrationBuilder.DropTable(
                name: "parcel_shortlist_items",
                schema: "land_intelligence");

            migrationBuilder.DropTable(
                name: "lease_interest_requests",
                schema: "land_intelligence");
        }
    }
}
