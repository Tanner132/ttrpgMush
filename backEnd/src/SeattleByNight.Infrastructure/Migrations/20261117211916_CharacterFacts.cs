using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CharacterFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_facts",
                columns: table => new
                {
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_instance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    set_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_facts", x => new { x.character_id, x.fact_id });
                    table.ForeignKey(
                        name: "FK_character_facts_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_character_facts_mission_instances_mission_instance_id",
                        column: x => x.mission_instance_id,
                        principalTable: "mission_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_character_facts_fact_id",
                table: "character_facts",
                column: "fact_id");

            migrationBuilder.CreateIndex(
                name: "ix_character_facts_mission_instance_id",
                table: "character_facts",
                column: "mission_instance_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_facts");
        }
    }
}
