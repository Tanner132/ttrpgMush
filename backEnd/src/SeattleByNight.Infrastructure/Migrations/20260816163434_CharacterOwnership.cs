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
            migrationBuilder.AddColumn<string>(
                name: "normalized_name",
                table: "characters",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "characters",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                DO $migration$
                DECLARE
                    legacy_owner_id uuid := '88888888-8888-8888-8888-888888888888';
                BEGIN
                    IF EXISTS (SELECT 1 FROM characters) THEN
                        IF EXISTS (
                            SELECT 1 FROM asp_net_users
                            WHERE id = legacy_owner_id
                              AND normalized_user_name <> 'LEGACY-CHARACTER-OWNER') THEN
                            RAISE EXCEPTION 'CharacterOwnership cannot reserve legacy owner ID %', legacy_owner_id;
                        END IF;

                        IF EXISTS (
                            SELECT 1 FROM asp_net_users
                            WHERE normalized_user_name = 'LEGACY-CHARACTER-OWNER'
                              AND id <> legacy_owner_id) THEN
                            RAISE EXCEPTION 'CharacterOwnership cannot reserve legacy owner username';
                        END IF;

                        INSERT INTO asp_net_users (
                            id, user_name, normalized_user_name, email, normalized_email,
                            email_confirmed, password_hash, security_stamp, concurrency_stamp,
                            phone_number, phone_number_confirmed, two_factor_enabled,
                            lockout_end, lockout_enabled, access_failed_count)
                        VALUES (
                            legacy_owner_id, 'legacy-character-owner', 'LEGACY-CHARACTER-OWNER',
                            'legacy-character-owner@invalid.local', 'LEGACY-CHARACTER-OWNER@INVALID.LOCAL',
                            false, NULL, gen_random_uuid()::text, gen_random_uuid()::text,
                            NULL, false, false, 'infinity', true, 0)
                        ON CONFLICT (id) DO NOTHING;

                        UPDATE characters
                        SET user_id = legacy_owner_id,
                            normalized_name = upper(name);
                    END IF;
                END
                $migration$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "normalized_name",
                table: "characters",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "characters",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

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
