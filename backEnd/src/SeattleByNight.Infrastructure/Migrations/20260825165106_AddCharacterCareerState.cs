using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeattleByNight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterCareerState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_action_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_action_receipts", x => x.id);
                    table.ForeignKey(
                        name: "FK_character_action_receipts_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "character_advancements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    target_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    details = table.Column<string>(type: "jsonb", nullable: false),
                    previous_value = table.Column<int>(type: "integer", nullable: true),
                    new_value = table.Column<int>(type: "integer", nullable: true),
                    karma_cost = table.Column<int>(type: "integer", nullable: false),
                    ruleset_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    catalog_version = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_advancements", x => x.id);
                    table.CheckConstraint("ck_character_advancements_karma_cost", "karma_cost >= 0");
                    table.ForeignKey(
                        name: "FK_character_advancements_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "character_career_states",
                columns: table => new
                {
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    career_document_schema_version = table.Column<int>(type: "integer", nullable: false),
                    progression = table.Column<string>(type: "jsonb", nullable: false),
                    current_karma = table.Column<int>(type: "integer", nullable: false),
                    current_nuyen = table.Column<int>(type: "integer", nullable: false),
                    lifetime_karma_earned = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_career_states", x => x.character_id);
                    table.CheckConstraint("ck_character_career_states_nonnegative", "current_karma >= 0 AND current_nuyen >= 0 AND lifetime_karma_earned >= 0");
                    table.CheckConstraint("ck_character_career_states_schema_version", "career_document_schema_version > 0");
                    table.ForeignKey(
                        name: "FK_character_career_states_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "character_inventory_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_item_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    catalog_collection = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ruleset_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    catalog_version = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    catalog_semantic_digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: true),
                    parameters = table.Column<string>(type: "jsonb", nullable: true),
                    purchase_price_nuyen = table.Column<int>(type: "integer", nullable: false),
                    acquisition_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    acquired_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_inventory_items", x => x.id);
                    table.CheckConstraint("ck_character_inventory_items_digest", "length(catalog_semantic_digest) = 64");
                    table.CheckConstraint("ck_character_inventory_items_price", "purchase_price_nuyen >= 0");
                    table.CheckConstraint("ck_character_inventory_items_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "FK_character_inventory_items_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "character_resource_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    balance_after = table.Column<int>(type: "integer", nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    advancement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    inventory_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_resource_transactions", x => x.id);
                    table.CheckConstraint("ck_character_resource_transactions_balance", "balance_after >= 0");
                    table.CheckConstraint("ck_character_resource_transactions_single_reference", "NOT (advancement_id IS NOT NULL AND inventory_item_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_character_resource_transactions_character_advancements_adva~",
                        column: x => x.advancement_id,
                        principalTable: "character_advancements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_character_resource_transactions_character_inventory_items_i~",
                        column: x => x.inventory_item_id,
                        principalTable: "character_inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_character_resource_transactions_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_character_action_receipts_character_id_request_id",
                table: "character_action_receipts",
                columns: new[] { "character_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_character_advancements_character_id_created_at_utc",
                table: "character_advancements",
                columns: new[] { "character_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_character_inventory_items_character_id_acquired_at_utc",
                table: "character_inventory_items",
                columns: new[] { "character_id", "acquired_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_character_resource_transactions_advancement_id",
                table: "character_resource_transactions",
                column: "advancement_id");

            migrationBuilder.CreateIndex(
                name: "IX_character_resource_transactions_character_id_created_at_utc",
                table: "character_resource_transactions",
                columns: new[] { "character_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_character_resource_transactions_inventory_item_id",
                table: "character_resource_transactions",
                column: "inventory_item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_action_receipts");

            migrationBuilder.DropTable(
                name: "character_career_states");

            migrationBuilder.DropTable(
                name: "character_resource_transactions");

            migrationBuilder.DropTable(
                name: "character_advancements");

            migrationBuilder.DropTable(
                name: "character_inventory_items");
        }
    }
}
