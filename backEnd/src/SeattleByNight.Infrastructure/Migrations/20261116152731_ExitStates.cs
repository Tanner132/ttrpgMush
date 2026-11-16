using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <summary>
    /// Workstream 8, phase 1: an exit's hidden and locked flags become one
    /// <c>states</c> array (locked, hidden, private, restricted). Existing
    /// flags are carried into the array before the columns are dropped.
    /// </summary>
    public partial class ExitStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "states",
                table: "room_exits",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            // Same order as ExitStates.Normalize: locked, then hidden.
            migrationBuilder.Sql("""
                UPDATE room_exits
                SET states = array_remove(ARRAY[
                    CASE WHEN is_locked THEN 'locked' END,
                    CASE WHEN is_hidden THEN 'hidden' END]::text[], NULL)
                WHERE is_locked OR is_hidden;
                """);

            migrationBuilder.DropColumn(
                name: "is_hidden",
                table: "room_exits");

            migrationBuilder.DropColumn(
                name: "is_locked",
                table: "room_exits");

            migrationBuilder.AddCheckConstraint(
                name: "ck_room_exits_states",
                table: "room_exits",
                sql: "states <@ ARRAY['locked','hidden','private','restricted']::text[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_room_exits_states",
                table: "room_exits");

            migrationBuilder.AddColumn<bool>(
                name: "is_hidden",
                table: "room_exits",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_locked",
                table: "room_exits",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Private and restricted have no column to go back to; a restricted
            // door was closed to entry, so it goes back as locked.
            migrationBuilder.Sql("""
                UPDATE room_exits
                SET is_hidden = 'hidden' = ANY(states),
                    is_locked = states && ARRAY['locked','private','restricted']::text[];
                """);

            migrationBuilder.DropColumn(
                name: "states",
                table: "room_exits");
        }
    }
}
