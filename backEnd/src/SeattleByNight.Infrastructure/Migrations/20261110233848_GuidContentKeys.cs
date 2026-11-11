using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GuidContentKeys : Migration
    {
        /// <inheritdoc />
        // WF-27: every authored record is keyed by a GUID. There is no upgrade
        // path for rows written under the old string keys — development
        // databases are recreated — so the casts below only have to work on
        // tables that hold no content-keyed rows yet. Npgsql scaffolds a bare
        // ALTER COLUMN TYPE, which Postgres refuses for text to uuid even on
        // an empty table, hence the explicit USING.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_trigger_fires_character_mission_key",
                table: "trigger_fires");

            migrationBuilder.DropColumn(
                name: "item_key",
                table: "world_item_instances");

            migrationBuilder.DropColumn(
                name: "trigger_key",
                table: "trigger_fires");

            migrationBuilder.DropColumn(
                name: "content_key",
                table: "rooms");

            migrationBuilder.AddColumn<int>(
                name: "discovery_threshold",
                table: "world_item_instances",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_hidden",
                table: "world_item_instances",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "item_id",
                table: "world_item_instances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "trigger_id",
                table: "trigger_fires",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("ALTER TABLE scene_sessions ALTER COLUMN scene_id TYPE uuid USING scene_id::uuid;");

            migrationBuilder.Sql("ALTER TABLE scene_sessions ALTER COLUMN current_node_id TYPE uuid USING current_node_id::uuid;");

            migrationBuilder.AddColumn<Guid>(
                name: "source_room_id",
                table: "rooms",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_interactable_id",
                table: "room_interactables",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("ALTER TABLE npc_instances ALTER COLUMN template_id TYPE uuid USING template_id::uuid;");

            migrationBuilder.Sql("ALTER TABLE npc_instances ALTER COLUMN scene_id TYPE uuid USING scene_id::uuid;");

            migrationBuilder.AddColumn<Guid>(
                name: "source_npc_id",
                table: "npc_instances",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("ALTER TABLE mission_instances ALTER COLUMN mission_id TYPE uuid USING mission_id::uuid;");

            migrationBuilder.Sql("ALTER TABLE game_content_definitions ALTER COLUMN content_key TYPE uuid USING content_key::uuid;");

            migrationBuilder.Sql("ALTER TABLE encounter_instances ALTER COLUMN encounter_id TYPE uuid USING encounter_id::uuid;");

            migrationBuilder.CreateIndex(
                name: "ux_trigger_fires_character_mission_key",
                table: "trigger_fires",
                columns: new[] { "character_id", "mission_instance_id", "trigger_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rooms_source_room_id",
                table: "rooms",
                column: "source_room_id");

            migrationBuilder.AddForeignKey(
                name: "FK_rooms_rooms_source_room_id",
                table: "rooms",
                column: "source_room_id",
                principalTable: "rooms",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rooms_rooms_source_room_id",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "ux_trigger_fires_character_mission_key",
                table: "trigger_fires");

            migrationBuilder.DropIndex(
                name: "IX_rooms_source_room_id",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "discovery_threshold",
                table: "world_item_instances");

            migrationBuilder.DropColumn(
                name: "is_hidden",
                table: "world_item_instances");

            migrationBuilder.DropColumn(
                name: "item_id",
                table: "world_item_instances");

            migrationBuilder.DropColumn(
                name: "trigger_id",
                table: "trigger_fires");

            migrationBuilder.DropColumn(
                name: "source_room_id",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "source_interactable_id",
                table: "room_interactables");

            migrationBuilder.DropColumn(
                name: "source_npc_id",
                table: "npc_instances");

            migrationBuilder.AddColumn<string>(
                name: "item_key",
                table: "world_item_instances",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "trigger_key",
                table: "trigger_fires",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("ALTER TABLE scene_sessions ALTER COLUMN scene_id TYPE character varying(100) USING scene_id::text;");

            migrationBuilder.Sql("ALTER TABLE scene_sessions ALTER COLUMN current_node_id TYPE character varying(100) USING current_node_id::text;");

            migrationBuilder.AddColumn<string>(
                name: "content_key",
                table: "rooms",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("ALTER TABLE npc_instances ALTER COLUMN template_id TYPE character varying(100) USING template_id::text;");

            migrationBuilder.Sql("ALTER TABLE npc_instances ALTER COLUMN scene_id TYPE character varying(100) USING scene_id::text;");

            migrationBuilder.Sql("ALTER TABLE mission_instances ALTER COLUMN mission_id TYPE character varying(100) USING mission_id::text;");

            migrationBuilder.Sql("ALTER TABLE game_content_definitions ALTER COLUMN content_key TYPE character varying(100) USING content_key::text;");

            migrationBuilder.Sql("ALTER TABLE encounter_instances ALTER COLUMN encounter_id TYPE character varying(100) USING encounter_id::text;");

            migrationBuilder.CreateIndex(
                name: "ux_trigger_fires_character_mission_key",
                table: "trigger_fires",
                columns: new[] { "character_id", "mission_instance_id", "trigger_key" },
                unique: true);
        }
    }
}
