using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CharacterOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing seeded characters predate ownership. They are disposable development
            // data and are recreated by the seeder with a valid owner on next startup.
            migrationBuilder.Sql("DELETE FROM characters;");

            migrationBuilder.AddColumn<string>(
                name: "normalized_name",
                table: "characters",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "characters",
                type: "uuid",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "ix_characters_normalized_name",
                table: "characters",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_characters_user_id",
                table: "characters",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_characters_asp_net_users_user_id",
                table: "characters",
                column: "user_id",
                principalTable: "asp_net_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_characters_asp_net_users_user_id",
                table: "characters");

            migrationBuilder.DropIndex(
                name: "ix_characters_normalized_name",
                table: "characters");

            migrationBuilder.DropIndex(
                name: "IX_characters_user_id",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "normalized_name",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "characters");
        }
    }
}
