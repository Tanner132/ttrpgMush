using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionsAndEncounterInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_rooms_map_layer_map_x_map_y",
                table: "rooms");

            migrationBuilder.AddColumn<Guid>(
                name: "encounter_instance_id",
                table: "rooms",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mission_instances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    objectives = table.Column<string>(type: "jsonb", nullable: false),
                    negotiated_nuyen = table.Column<int>(type: "integer", nullable: true),
                    accepted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mission_instances", x => x.id);
                    table.ForeignKey(
                        name: "FK_mission_instances_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "encounter_instances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    encounter_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    mission_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entry_room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_activity_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    encounter_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "world_item_instances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    mission_instance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    encounter_instance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    room_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_character_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_world_item_instances", x => x.id);
                    table.CheckConstraint("ck_world_item_instances_one_location", "(room_id IS NULL) <> (owner_character_id IS NULL)");
                    table.ForeignKey(
                        name: "FK_world_item_instances_characters_owner_character_id",
                        column: x => x.owner_character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_world_item_instances_encounter_instances_encounter_instance~",
                        column: x => x.encounter_instance_id,
                        principalTable: "encounter_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_world_item_instances_mission_instances_mission_instance_id",
                        column: x => x.mission_instance_id,
                        principalTable: "mission_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_world_item_instances_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rooms_encounter_instance",
                table: "rooms",
                column: "encounter_instance_id");

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

            migrationBuilder.CreateIndex(
                name: "ix_mission_instances_character_mission",
                table: "mission_instances",
                columns: new[] { "character_id", "mission_id" });

            migrationBuilder.CreateIndex(
                name: "IX_world_item_instances_encounter_instance_id",
                table: "world_item_instances",
                column: "encounter_instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_world_item_instances_mission_instance",
                table: "world_item_instances",
                column: "mission_instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_world_item_instances_owner",
                table: "world_item_instances",
                column: "owner_character_id");

            migrationBuilder.CreateIndex(
                name: "ix_world_item_instances_room",
                table: "world_item_instances",
                column: "room_id");

            migrationBuilder.AddForeignKey(
                name: "FK_rooms_encounter_instances_encounter_instance_id",
                table: "rooms",
                column: "encounter_instance_id",
                principalTable: "encounter_instances",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rooms_encounter_instances_encounter_instance_id",
                table: "rooms");

            migrationBuilder.DropTable(
                name: "encounter_participants");

            migrationBuilder.DropTable(
                name: "world_item_instances");

            migrationBuilder.DropTable(
                name: "encounter_instances");

            migrationBuilder.DropTable(
                name: "mission_instances");

            migrationBuilder.DropIndex(
                name: "ix_rooms_encounter_instance",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "ux_rooms_map_layer_map_x_map_y",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "encounter_instance_id",
                table: "rooms");

            migrationBuilder.CreateIndex(
                name: "ux_rooms_map_layer_map_x_map_y",
                table: "rooms",
                columns: new[] { "map_layer", "map_x", "map_y" },
                unique: true);
        }
    }
}
