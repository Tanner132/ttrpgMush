using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SharedWorld : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Workstream 8, phase 2: one shared map. Data first, while the
            // run-copy columns still exist to find the copies by.

            // Anyone standing in a run's copy goes to the starting room.
            migrationBuilder.Sql("""
                UPDATE characters SET current_room_id = '44444444-4444-4444-4444-444444444444'
                WHERE current_room_id IN (SELECT id FROM rooms WHERE encounter_instance_id IS NOT NULL);
                """);

            // Open conversations end, and items go: job items are remade one
            // per job item (JobItemSync) from the content imported at start-up.
            migrationBuilder.Sql("DELETE FROM scene_sessions;");
            migrationBuilder.Sql("DELETE FROM world_item_instances;");

            // The run copies and everything in them.
            migrationBuilder.Sql("""
                DELETE FROM room_exits
                WHERE source_room_id IN (SELECT id FROM rooms WHERE encounter_instance_id IS NOT NULL)
                   OR destination_room_id IN (SELECT id FROM rooms WHERE encounter_instance_id IS NOT NULL);
                DELETE FROM npc_instances
                WHERE room_id IN (SELECT id FROM rooms WHERE encounter_instance_id IS NOT NULL);
                DELETE FROM room_interactables
                WHERE room_id IN (SELECT id FROM rooms WHERE encounter_instance_id IS NOT NULL);
                DELETE FROM chat_messages
                WHERE room_id IN (SELECT id FROM rooms WHERE encounter_instance_id IS NOT NULL);
                DELETE FROM room_visits
                WHERE room_id IN (SELECT id FROM rooms WHERE encounter_instance_id IS NOT NULL);
                DELETE FROM rooms WHERE encounter_instance_id IS NOT NULL;
                """);

            // Every room is Public until player homes (workstream 7).
            migrationBuilder.Sql("UPDATE rooms SET access_type = 'Public' WHERE access_type <> 'Public';");

            // Runs under way were made from job content that no longer exists.
            migrationBuilder.Sql("""
                UPDATE mission_instances SET status = 'Abandoned', updated_at_utc = now()
                WHERE status IN ('Accepted', 'InProgress', 'ReadyToTurnIn');
                """);

            // Old job content is discarded (owner, 2026-11-12): every job site,
            // every job, and every conversation that names a job. Stat blocks,
            // tests, other conversations and rooms' own events stay. The
            // seeder imports the rewritten shipped job on the next start.
            migrationBuilder.Sql("""
                DELETE FROM game_content_definitions
                WHERE kind IN ('Encounter', 'Mission')
                   OR (kind = 'Scene' AND (draft_payload::text LIKE '%"missionId"%'
                       OR COALESCE(published_payload::text, '') LIKE '%"missionId"%'));
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_rooms_encounter_instances_encounter_instance_id",
                table: "rooms");

            migrationBuilder.DropForeignKey(
                name: "FK_rooms_rooms_source_room_id",
                table: "rooms");

            migrationBuilder.DropForeignKey(
                name: "FK_world_item_instances_encounter_instances_encounter_instance~",
                table: "world_item_instances");

            migrationBuilder.DropForeignKey(
                name: "FK_world_item_instances_mission_instances_mission_instance_id",
                table: "world_item_instances");

            migrationBuilder.DropTable(
                name: "encounter_participants");

            migrationBuilder.DropTable(
                name: "encounter_instances");

            migrationBuilder.DropIndex(
                name: "IX_world_item_instances_encounter_instance_id",
                table: "world_item_instances");

            migrationBuilder.DropIndex(
                name: "ix_world_item_instances_mission_instance",
                table: "world_item_instances");

            migrationBuilder.DropCheckConstraint(
                name: "ck_world_item_instances_one_location",
                table: "world_item_instances");

            migrationBuilder.DropIndex(
                name: "ix_rooms_encounter_instance",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "IX_rooms_source_room_id",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "ux_rooms_map_layer_map_x_map_y",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "encounter_instance_id",
                table: "world_item_instances");

            migrationBuilder.DropColumn(
                name: "mission_instance_id",
                table: "world_item_instances");

            migrationBuilder.DropColumn(
                name: "encounter_instance_id",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "source_room_id",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "source_interactable_id",
                table: "room_interactables");

            migrationBuilder.DropColumn(
                name: "source_npc_id",
                table: "npc_instances");

            migrationBuilder.AddColumn<Guid>(
                name: "mission_id",
                table: "world_item_instances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_world_item_instances_mission",
                table: "world_item_instances",
                column: "mission_id");

            migrationBuilder.CreateIndex(
                name: "ux_world_item_instances_item",
                table: "world_item_instances",
                column: "item_id",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_world_item_instances_one_location",
                table: "world_item_instances",
                sql: "room_id IS NULL OR owner_character_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_rooms_map_layer_map_x_map_y",
                table: "rooms",
                columns: new[] { "map_layer", "map_x", "map_y" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_world_item_instances_mission",
                table: "world_item_instances");

            migrationBuilder.DropIndex(
                name: "ux_world_item_instances_item",
                table: "world_item_instances");

            migrationBuilder.DropCheckConstraint(
                name: "ck_world_item_instances_one_location",
                table: "world_item_instances");

            migrationBuilder.DropIndex(
                name: "ux_rooms_map_layer_map_x_map_y",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "mission_id",
                table: "world_item_instances");

            migrationBuilder.AddColumn<Guid>(
                name: "encounter_instance_id",
                table: "world_item_instances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "mission_instance_id",
                table: "world_item_instances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "encounter_instance_id",
                table: "rooms",
                type: "uuid",
                nullable: true);

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

            migrationBuilder.AddColumn<Guid>(
                name: "source_npc_id",
                table: "npc_instances",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "encounter_instances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_activity_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    mission_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encounter_instances", x => x.id);
                    table.ForeignKey(
                        name: "FK_encounter_instances_mission_instances_mission_instance_id",
                        column: x => x.mission_instance_id,
                        principalTable: "mission_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "encounter_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encounter_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encounter_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_encounter_participants_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_encounter_participants_encounter_instances_encounter_instan~",
                        column: x => x.encounter_instance_id,
                        principalTable: "encounter_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_world_item_instances_encounter_instance_id",
                table: "world_item_instances",
                column: "encounter_instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_world_item_instances_mission_instance",
                table: "world_item_instances",
                column: "mission_instance_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_world_item_instances_one_location",
                table: "world_item_instances",
                sql: "(room_id IS NULL) <> (owner_character_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_rooms_encounter_instance",
                table: "rooms",
                column: "encounter_instance_id");

            migrationBuilder.CreateIndex(
                name: "IX_rooms_source_room_id",
                table: "rooms",
                column: "source_room_id");

            migrationBuilder.CreateIndex(
                name: "ux_rooms_map_layer_map_x_map_y",
                table: "rooms",
                columns: new[] { "map_layer", "map_x", "map_y" },
                unique: true,
                filter: "encounter_instance_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_encounter_instances_mission_instance",
                table: "encounter_instances",
                column: "mission_instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_encounter_instances_status",
                table: "encounter_instances",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_encounter_participants_character",
                table: "encounter_participants",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "ux_encounter_participants_instance_character",
                table: "encounter_participants",
                columns: new[] { "encounter_instance_id", "character_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_rooms_encounter_instances_encounter_instance_id",
                table: "rooms",
                column: "encounter_instance_id",
                principalTable: "encounter_instances",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_rooms_rooms_source_room_id",
                table: "rooms",
                column: "source_room_id",
                principalTable: "rooms",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_world_item_instances_encounter_instances_encounter_instance~",
                table: "world_item_instances",
                column: "encounter_instance_id",
                principalTable: "encounter_instances",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_world_item_instances_mission_instances_mission_instance_id",
                table: "world_item_instances",
                column: "mission_instance_id",
                principalTable: "mission_instances",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
