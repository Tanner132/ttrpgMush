using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class JobClock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "defeated_at_utc",
                table: "npc_instances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "return_delays",
                table: "npc_instances",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "return_due_at_utc",
                table: "npc_instances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "accept_group_id",
                table: "mission_instances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "job_states",
                columns: table => new
                {
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    opened_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deadline_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_by_character_id = table.Column<Guid>(type: "uuid", nullable: true),
                    first_accept_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_states", x => x.mission_id);
                    table.CheckConstraint("ck_job_states_status", "status IN ('Available','Open','Aftermath')");
                });

            // Every acceptance so far was a lone runner's: its group is itself.
            migrationBuilder.Sql("UPDATE mission_instances SET accept_group_id = id;");

            // Jobs already being run get an open clock with the default
            // deadline from now, so nobody's run expires on the upgrade.
            // Every other job gets its Available row at start-up (JobItemSync).
            migrationBuilder.Sql("""
                INSERT INTO job_states (mission_id, status, opened_at_utc, deadline_at_utc, first_accept_group_id, version, updated_at_utc)
                SELECT DISTINCT ON (mission_id)
                       mission_id, 'Open', accepted_at_utc, now() + interval '3 hours', id, gen_random_uuid(), now()
                FROM mission_instances
                WHERE status IN ('Accepted', 'InProgress', 'ReadyToTurnIn')
                ORDER BY mission_id, accepted_at_utc;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_mission_instances_mission_status",
                table: "mission_instances",
                columns: new[] { "mission_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_job_states_status",
                table: "job_states",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_states");

            migrationBuilder.DropIndex(
                name: "ix_mission_instances_mission_status",
                table: "mission_instances");

            migrationBuilder.DropColumn(
                name: "defeated_at_utc",
                table: "npc_instances");

            migrationBuilder.DropColumn(
                name: "return_delays",
                table: "npc_instances");

            migrationBuilder.DropColumn(
                name: "return_due_at_utc",
                table: "npc_instances");

            migrationBuilder.DropColumn(
                name: "accept_group_id",
                table: "mission_instances");
        }
    }
}
