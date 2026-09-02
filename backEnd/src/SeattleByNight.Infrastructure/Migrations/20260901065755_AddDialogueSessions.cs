using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDialogueSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dialogue_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    npc_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dialogue_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    current_node_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    pending_negotiated_nuyen = table.Column<int>(type: "integer", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dialogue_sessions");
        }
    }
}
