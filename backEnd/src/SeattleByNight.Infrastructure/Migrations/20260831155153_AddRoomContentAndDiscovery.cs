using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomContentAndDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_discoveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    discovered_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_discoveries", x => x.id);
                    table.ForeignKey(
                        name: "FK_character_discoveries_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "npc_instances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    physical_damage = table.Column<int>(type: "integer", nullable: false),
                    stun_damage = table.Column<int>(type: "integer", nullable: false),
                    awareness = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_npc_instances", x => x.id);
                    table.ForeignKey(
                        name: "FK_npc_instances_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_interactables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    discovery_threshold = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_interactables", x => x.id);
                    table.ForeignKey(
                        name: "FK_room_interactables_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_character_discoveries_character_subject",
                table: "character_discoveries",
                columns: new[] { "character_id", "subject_type", "subject_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_npc_instances_room",
                table: "npc_instances",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_room_interactables_room",
                table: "room_interactables",
                column: "room_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_discoveries");

            migrationBuilder.DropTable(
                name: "npc_instances");

            migrationBuilder.DropTable(
                name: "room_interactables");
        }
    }
}
