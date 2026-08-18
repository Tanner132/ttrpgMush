using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeTranscriptVisibilityAndConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_room_visits_transcript_visibility",
                table: "room_visits",
                columns: new[] { "play_session_id", "room_id", "entered_at_utc", "left_at_utc" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_room_visits_interval",
                table: "room_visits",
                sql: "left_at_utc IS NULL OR left_at_utc >= entered_at_utc");

            migrationBuilder.AddCheckConstraint(
                name: "ck_chat_messages_type",
                table: "chat_messages",
                sql: "type IN ('Say','Emote','Roll')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_room_visits_transcript_visibility",
                table: "room_visits");

            migrationBuilder.DropCheckConstraint(
                name: "ck_room_visits_interval",
                table: "room_visits");

            migrationBuilder.DropCheckConstraint(
                name: "ck_chat_messages_type",
                table: "chat_messages");
        }
    }
}
