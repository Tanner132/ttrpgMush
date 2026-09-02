using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    // Milestone 7 section 4: the two-layer NPC model. The base stat block
    // moved out of code and into the content document (imported by
    // GameContentSeeder on the next startup); these columns hold what a
    // placement pins on top of it. All three are nullable because absent
    // means "whatever the template says".
    public partial class AddNpcPlacementOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "npc_instances",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "overrides",
                table: "npc_instances",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scene_id",
                table: "npc_instances",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "description",
                table: "npc_instances");

            migrationBuilder.DropColumn(
                name: "overrides",
                table: "npc_instances");

            migrationBuilder.DropColumn(
                name: "scene_id",
                table: "npc_instances");
        }
    }
}
