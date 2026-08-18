using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PersistCharacterCreationDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_characters_user_id",
                table: "characters");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "finalized_at_utc",
                table: "characters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lifecycle_state",
                table: "characters",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "character_creation_drafts",
                columns: table => new
                {
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ruleset_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    catalog_version = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    catalog_semantic_digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    creation_method_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    document_schema_version = table.Column<int>(type: "integer", nullable: false),
                    selections = table.Column<string>(type: "jsonb", nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_creation_drafts", x => x.character_id);
                    table.CheckConstraint("ck_character_creation_drafts_digest", "length(catalog_semantic_digest) = 64");
                    table.CheckConstraint("ck_character_creation_drafts_document_schema_version", "document_schema_version > 0");
                    table.ForeignKey(
                        name: "FK_character_creation_drafts_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "character_sheets",
                columns: table => new
                {
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ruleset_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    catalog_version = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    catalog_semantic_digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    creation_method_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sheet_schema_version = table.Column<int>(type: "integer", nullable: false),
                    canonical_sheet = table.Column<string>(type: "jsonb", nullable: false),
                    source_draft_digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    finalized_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_sheets", x => x.character_id);
                    table.CheckConstraint("ck_character_sheets_digests", "length(catalog_semantic_digest) = 64 AND length(source_draft_digest) = 64");
                    table.CheckConstraint("ck_character_sheets_schema_version", "sheet_schema_version > 0");
                    table.ForeignKey(
                        name: "FK_character_sheets_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                UPDATE characters
                SET lifecycle_state = 'Finalized',
                    finalized_at_utc = created_at_utc;

                INSERT INTO character_sheets (
                    character_id,
                    ruleset_id,
                    catalog_version,
                    catalog_semantic_digest,
                    creation_method_id,
                    sheet_schema_version,
                    canonical_sheet,
                    source_draft_digest,
                    finalized_at_utc,
                    kind)
                SELECT
                    id,
                    'legacy',
                    '0.0.0',
                    '44136FA355B3678A1146AD16F7E8649E94FB4FC21FE77E8310C060F61CAAFF8A',
                    'legacy-import',
                    1,
                    '{"legacy":true}'::jsonb,
                    '44136FA355B3678A1146AD16F7E8649E94FB4FC21FE77E8310C060F61CAAFF8A',
                    created_at_utc,
                    'Legacy'
                FROM characters;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_characters_user_id_lifecycle_state",
                table: "characters",
                columns: new[] { "user_id", "lifecycle_state" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_characters_lifecycle_finalized_at",
                table: "characters",
                sql: "(lifecycle_state = 'Draft' AND finalized_at_utc IS NULL) OR (lifecycle_state = 'Finalized' AND finalized_at_utc IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_creation_drafts");

            migrationBuilder.DropTable(
                name: "character_sheets");

            migrationBuilder.DropIndex(
                name: "ix_characters_user_id_lifecycle_state",
                table: "characters");

            migrationBuilder.DropCheckConstraint(
                name: "ck_characters_lifecycle_finalized_at",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "finalized_at_utc",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "lifecycle_state",
                table: "characters");

            migrationBuilder.CreateIndex(
                name: "IX_characters_user_id",
                table: "characters",
                column: "user_id");
        }
    }
}
