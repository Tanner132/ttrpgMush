using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MapsAndRoomLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_rooms_map_layer_map_x_map_y",
                table: "rooms");

            migrationBuilder.AddColumn<bool>(
                name: "is_live",
                table: "rooms",
                type: "boolean",
                nullable: false,
                // WF-60: every room built before publishing existed is live.
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "map_id",
                table: "rooms",
                type: "uuid",
                nullable: false,
                // WF-41: every existing room moves to the Seattle map.
                defaultValue: new Guid("5ea771e0-0000-4000-8000-000000000001"));

            migrationBuilder.CreateTable(
                name: "maps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maps", x => x.id);
                });

            migrationBuilder.Sql(
                "INSERT INTO maps (id, name, created_at_utc, version) "
                + "VALUES ('5ea771e0-0000-4000-8000-000000000001', 'Seattle', now(), gen_random_uuid());");

            migrationBuilder.CreateTable(
                name: "travel_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_travel_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_travel_links_rooms_destination_room_id",
                        column: x => x.destination_room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_travel_links_rooms_source_room_id",
                        column: x => x.source_room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_rooms_map_id_map_layer_map_x_map_y",
                table: "rooms",
                columns: new[] { "map_id", "map_layer", "map_x", "map_y" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_maps_name",
                table: "maps",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_travel_links_destination",
                table: "travel_links",
                column: "destination_room_id");

            migrationBuilder.CreateIndex(
                name: "ux_travel_links_source_name",
                table: "travel_links",
                columns: new[] { "source_room_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_rooms_maps_map_id",
                table: "rooms",
                column: "map_id",
                principalTable: "maps",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rooms_maps_map_id",
                table: "rooms");

            migrationBuilder.DropTable(
                name: "maps");

            migrationBuilder.DropTable(
                name: "travel_links");

            migrationBuilder.DropIndex(
                name: "ux_rooms_map_id_map_layer_map_x_map_y",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "is_live",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "map_id",
                table: "rooms");

            migrationBuilder.CreateIndex(
                name: "ux_rooms_map_layer_map_x_map_y",
                table: "rooms",
                columns: new[] { "map_layer", "map_x", "map_y" },
                unique: true);
        }
    }
}
