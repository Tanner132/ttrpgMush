using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTriggersAndScenes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Conversational state, not history: a scene session records where
            // a character currently stands in one conversation, and the audit
            // log keeps the record of what every choice did. Recreating the
            // table costs at most an open conversation, which the next talk
            // action reopens.
            migrationBuilder.DropTable(
                name: "dialogue_sessions");

            // Milestone 7 renamed the Dialogue content kind to Scene and its
            // payload's endsDialogue flag to endsScene, so the seeded rows are
            // no longer loadable. Nothing can have edited them — the builder
            // UI does not exist yet — so they are dropped and re-imported from
            // the bundle by GameContentSeeder on the next startup.
            migrationBuilder.Sql("DELETE FROM game_content_definitions WHERE kind = 'Dialogue';");

            migrationBuilder.AddColumn<string>(
                name: "content_key",
                table: "rooms",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "scene_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    npc_instance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scene_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    current_node_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    pending_negotiated_nuyen = table.Column<int>(type: "integer", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scene_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_scene_sessions_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_scene_sessions_npc_instances_npc_instance_id",
                        column: x => x.npc_instance_id,
                        principalTable: "npc_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trigger_fires",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trigger_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    fired_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trigger_fires", x => x.id);
                    table.ForeignKey(
                        name: "FK_trigger_fires_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_trigger_fires_mission_instances_mission_instance_id",
                        column: x => x.mission_instance_id,
                        principalTable: "mission_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scene_sessions_npc_instance_id",
                table: "scene_sessions",
                column: "npc_instance_id");

            migrationBuilder.CreateIndex(
                name: "ux_scene_sessions_character",
                table: "scene_sessions",
                column: "character_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trigger_fires_mission_instance_id",
                table: "trigger_fires",
                column: "mission_instance_id");

            migrationBuilder.CreateIndex(
                name: "ux_trigger_fires_character_mission_key",
                table: "trigger_fires",
                columns: new[] { "character_id", "mission_instance_id", "trigger_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scene_sessions");

            migrationBuilder.DropTable(
                name: "trigger_fires");

            migrationBuilder.DropColumn(
                name: "content_key",
                table: "rooms");

            migrationBuilder.CreateTable(
                name: "dialogue_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    current_node_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    dialogue_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    npc_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pending_negotiated_nuyen = table.Column<int>(type: "integer", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dialogue_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_dialogue_sessions_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dialogue_sessions_npc_instances_npc_instance_id",
                        column: x => x.npc_instance_id,
                        principalTable: "npc_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dialogue_sessions_npc_instance_id",
                table: "dialogue_sessions",
                column: "npc_instance_id");

            migrationBuilder.CreateIndex(
                name: "ux_dialogue_sessions_character",
                table: "dialogue_sessions",
                column: "character_id",
                unique: true);
        }
    }
}
