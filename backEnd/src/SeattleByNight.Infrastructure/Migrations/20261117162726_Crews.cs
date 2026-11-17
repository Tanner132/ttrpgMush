using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Crews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "crew_id",
                table: "mission_instances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "crew_id",
                table: "characters",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "crew_invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inviter_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitee_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crew_invitations", x => x.id);
                    table.ForeignKey(
                        name: "FK_crew_invitations_characters_invitee_character_id",
                        column: x => x.invitee_character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_crew_invitations_characters_inviter_character_id",
                        column: x => x.inviter_character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "crews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crews", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_characters_crew_id",
                table: "characters",
                column: "crew_id");

            migrationBuilder.CreateIndex(
                name: "ix_crew_invitations_invitee",
                table: "crew_invitations",
                column: "invitee_character_id");

            migrationBuilder.CreateIndex(
                name: "IX_crew_invitations_inviter_character_id",
                table: "crew_invitations",
                column: "inviter_character_id");

            migrationBuilder.AddForeignKey(
                name: "FK_characters_crews_crew_id",
                table: "characters",
                column: "crew_id",
                principalTable: "crews",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_characters_crews_crew_id",
                table: "characters");

            migrationBuilder.DropTable(
                name: "crew_invitations");

            migrationBuilder.DropTable(
                name: "crews");

            migrationBuilder.DropIndex(
                name: "ix_characters_crew_id",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "crew_id",
                table: "mission_instances");

            migrationBuilder.DropColumn(
                name: "crew_id",
                table: "characters");
        }
    }
}
