using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThingsInRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_world_item_instances_item",
                table: "world_item_instances");

            migrationBuilder.AlterColumn<Guid>(
                name: "mission_id",
                table: "world_item_instances",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "container_id",
                table: "world_item_instances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_locked",
                table: "room_interactables",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "key_item_id",
                table: "room_interactables",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "room_interactables",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "inspect");

            migrationBuilder.AddColumn<string>(
                name: "read_text",
                table: "room_interactables",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "discovery_threshold",
                table: "room_exits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "key_item_id",
                table: "room_exits",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "discovery_threshold",
                table: "npc_instances",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_hidden",
                table: "npc_instances",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "map_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    container_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    is_unique = table.Column<bool>(type: "boolean", nullable: false),
                    respawn_minutes = table.Column<int>(type: "integer", nullable: true),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    discovery_threshold = table.Column<int>(type: "integer", nullable: false),
                    taken_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_map_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_map_items_room_interactables_container_id",
                        column: x => x.container_id,
                        principalTable: "room_interactables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_map_items_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_world_item_instances_item",
                table: "world_item_instances",
                column: "item_id",
                unique: true,
                filter: "mission_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_map_items_container_id",
                table: "map_items",
                column: "container_id");

            migrationBuilder.CreateIndex(
                name: "ix_map_items_room",
                table: "map_items",
                column: "room_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "map_items");

            migrationBuilder.DropIndex(
                name: "ux_world_item_instances_item",
                table: "world_item_instances");

            migrationBuilder.DropColumn(
                name: "container_id",
                table: "world_item_instances");

            migrationBuilder.DropColumn(
                name: "is_locked",
                table: "room_interactables");

            migrationBuilder.DropColumn(
                name: "key_item_id",
                table: "room_interactables");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "room_interactables");

            migrationBuilder.DropColumn(
                name: "read_text",
                table: "room_interactables");

            migrationBuilder.DropColumn(
                name: "discovery_threshold",
                table: "room_exits");

            migrationBuilder.DropColumn(
                name: "key_item_id",
                table: "room_exits");

            migrationBuilder.DropColumn(
                name: "discovery_threshold",
                table: "npc_instances");

            migrationBuilder.DropColumn(
                name: "is_hidden",
                table: "npc_instances");

            migrationBuilder.AlterColumn<Guid>(
                name: "mission_id",
                table: "world_item_instances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_world_item_instances_item",
                table: "world_item_instances",
                column: "item_id",
                unique: true);
        }
    }
}
