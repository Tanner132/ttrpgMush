using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class QualityRemovalRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quality_removal_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quality_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    quality_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    karma_cost = table.Column<int>(type: "integer", nullable: false),
                    requirement = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    explanation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    hold_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    advancement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    refund_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    requested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quality_removal_requests", x => x.id);
                    table.CheckConstraint("ck_quality_removal_requests_karma_cost", "karma_cost > 0");
                    table.ForeignKey(
                        name: "FK_quality_removal_requests_character_advancements_advancement~",
                        column: x => x.advancement_id,
                        principalTable: "character_advancements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quality_removal_requests_character_resource_transactions_ho~",
                        column: x => x.hold_transaction_id,
                        principalTable: "character_resource_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quality_removal_requests_character_resource_transactions_re~",
                        column: x => x.refund_transaction_id,
                        principalTable: "character_resource_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quality_removal_requests_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quality_removal_requests_advancement_id",
                table: "quality_removal_requests",
                column: "advancement_id");

            migrationBuilder.CreateIndex(
                name: "IX_quality_removal_requests_character_id_quality_key",
                table: "quality_removal_requests",
                columns: new[] { "character_id", "quality_key" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_quality_removal_requests_hold_transaction_id",
                table: "quality_removal_requests",
                column: "hold_transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_quality_removal_requests_refund_transaction_id",
                table: "quality_removal_requests",
                column: "refund_transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_quality_removal_requests_status_requested_at_utc",
                table: "quality_removal_requests",
                columns: new[] { "status", "requested_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quality_removal_requests");
        }
    }
}
