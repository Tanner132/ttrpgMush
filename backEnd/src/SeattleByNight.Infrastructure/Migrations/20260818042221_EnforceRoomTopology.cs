using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceRoomTopology : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO rooms (
                    id, name, description, access_type, map_x, map_y, map_layer, created_at_utc, version)
                VALUES (
                    '44444444-4444-4444-4444-444444444444',
                    'New Character Room',
                    'A featureless liminal space where newly minted runners first open their eyes.',
                    'Public', 0, 0, -1, now(), gen_random_uuid())
                ON CONFLICT (id) DO NOTHING;

                UPDATE rooms SET map_x = 0, map_y = 0, map_layer = 0
                WHERE id = '11111111-1111-1111-1111-111111111111';
                UPDATE rooms SET map_x = 1, map_y = 0, map_layer = 0
                WHERE id = '22222222-2222-2222-2222-222222222222';
                UPDATE rooms SET map_x = 0, map_y = 1, map_layer = 0
                WHERE id = '33333333-3333-3333-3333-333333333333';
                UPDATE rooms SET map_x = 0, map_y = 0, map_layer = -1
                WHERE id = '44444444-4444-4444-4444-444444444444';

                UPDATE room_exits SET direction = 'east'
                WHERE id = 'dddddddd-dddd-dddd-dddd-000000000001';
                UPDATE room_exits SET direction = 'west'
                WHERE id = 'dddddddd-dddd-dddd-dddd-000000000002';
                UPDATE room_exits SET direction = 'north'
                WHERE id = 'dddddddd-dddd-dddd-dddd-000000000003';

                UPDATE room_exits SET direction = lower(btrim(direction));

                DO $migration$
                DECLARE
                    incomplete_room_ids text;
                    invalid_exits text;
                    duplicate_directions text;
                BEGIN
                    SELECT string_agg(id::text, ', ' ORDER BY id::text)
                    INTO incomplete_room_ids
                    FROM rooms
                    WHERE map_x IS NULL OR map_y IS NULL OR map_layer IS NULL;

                    IF incomplete_room_ids IS NOT NULL THEN
                        RAISE EXCEPTION 'EnforceRoomTopology requires complete coordinates. Assign map_x, map_y, and map_layer to room IDs: %', incomplete_room_ids;
                    END IF;

                    SELECT string_agg(id::text || ' (' || direction || ')', ', ' ORDER BY id::text)
                    INTO invalid_exits
                    FROM room_exits
                    WHERE direction NOT IN ('north','northeast','east','southeast','south','southwest','west','northwest','up','down');

                    IF invalid_exits IS NOT NULL THEN
                        RAISE EXCEPTION 'EnforceRoomTopology requires approved exit directions. Update exit IDs: %', invalid_exits;
                    END IF;

                    SELECT string_agg(source_room_id::text || ' (' || direction || ')', ', ' ORDER BY source_room_id::text, direction)
                    INTO duplicate_directions
                    FROM (
                        SELECT source_room_id, direction
                        FROM room_exits
                        GROUP BY source_room_id, direction
                        HAVING count(*) > 1
                    ) duplicates;

                    IF duplicate_directions IS NOT NULL THEN
                        RAISE EXCEPTION 'EnforceRoomTopology requires one exit per source and direction. Resolve duplicates for: %', duplicate_directions;
                    END IF;
                END
                $migration$;
                """);

            migrationBuilder.DropIndex(
                name: "ix_room_exits_source_room_id",
                table: "room_exits");

            migrationBuilder.DropColumn(
                name: "name",
                table: "room_exits");

            migrationBuilder.AlterColumn<int>(
                name: "map_y",
                table: "rooms",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "map_x",
                table: "rooms",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "map_layer",
                table: "rooms",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "direction",
                table: "room_exits",
                type: "character varying(9)",
                maxLength: 9,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.CreateIndex(
                name: "ux_rooms_map_layer_map_x_map_y",
                table: "rooms",
                columns: new[] { "map_layer", "map_x", "map_y" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_room_exits_source_room_id_direction",
                table: "room_exits",
                columns: new[] { "source_room_id", "direction" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_room_exits_direction",
                table: "room_exits",
                sql: "direction IN ('north','northeast','east','southeast','south','southwest','west','northwest','up','down')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_rooms_map_layer_map_x_map_y",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "ux_room_exits_source_room_id_direction",
                table: "room_exits");

            migrationBuilder.DropCheckConstraint(
                name: "ck_room_exits_direction",
                table: "room_exits");

            migrationBuilder.AlterColumn<int>(
                name: "map_y",
                table: "rooms",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "map_x",
                table: "rooms",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "map_layer",
                table: "rooms",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "direction",
                table: "room_exits",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(9)",
                oldMaxLength: 9);

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "room_exits",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_room_exits_source_room_id",
                table: "room_exits",
                column: "source_room_id");
        }
    }
}
