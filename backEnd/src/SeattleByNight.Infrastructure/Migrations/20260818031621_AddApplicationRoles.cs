using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO asp_net_roles (id, name, normalized_name, concurrency_stamp)
                VALUES
                    ('77777777-7777-7777-7777-000000000001', 'Administrator', 'ADMINISTRATOR', '77777777-7777-7777-7777-100000000001'),
                    ('77777777-7777-7777-7777-000000000002', 'WorldBuilder', 'WORLDBUILDER', '77777777-7777-7777-7777-100000000002'),
                    ('77777777-7777-7777-7777-000000000003', 'Moderator', 'MODERATOR', '77777777-7777-7777-7777-100000000003')
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM asp_net_roles AS roles
                WHERE roles.id IN (
                    '77777777-7777-7777-7777-000000000001',
                    '77777777-7777-7777-7777-000000000002',
                    '77777777-7777-7777-7777-000000000003')
                  AND NOT EXISTS (
                      SELECT 1
                      FROM asp_net_user_roles AS user_roles
                      WHERE user_roles.role_id = roles.id);
                """);
        }
    }
}
