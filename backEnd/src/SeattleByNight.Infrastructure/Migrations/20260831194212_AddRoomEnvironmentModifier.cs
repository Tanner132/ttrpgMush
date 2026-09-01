using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomEnvironmentModifier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EnvironmentModifier",
                table: "rooms",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnvironmentModifier",
                table: "rooms");
        }
    }
}
