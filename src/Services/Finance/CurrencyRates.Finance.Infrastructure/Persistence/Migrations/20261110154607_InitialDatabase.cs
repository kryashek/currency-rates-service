using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CurrencyRates.Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

            migrationBuilder.CreateTable(
                name: "currency",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_available = table.Column<bool>(type: "boolean", nullable: false),
                    rate = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currency", x => x.id);
                    table.CheckConstraint("ck_currency_code", "code COLLATE \"C\" ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_currency_id", "id COLLATE \"C\" ~ '^R[0-9]{1,31}$'");
                    table.CheckConstraint("ck_currency_name", "name ~ U&'[^\\0009\\000A\\000B\\000C\\000D\\0020\\0085\\00A0\\1680\\2000\\2001\\2002\\2003\\2004\\2005\\2006\\2007\\2008\\2009\\200A\\2028\\2029\\202F\\205F\\3000]'");
                    table.CheckConstraint("ck_currency_rate", "(is_available AND rate IS NOT NULL AND rate > 0 AND rate < 1000000000000) OR (NOT is_available AND rate IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "import_batch",
                schema: "finance",
                columns: table => new
                {
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    expected_snapshot_version = table.Column<decimal>(type: "numeric(20,0)", precision: 20, scale: 0, nullable: false),
                    source_date = table.Column<DateOnly>(type: "date", nullable: false),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    outcome = table.Column<int>(type: "integer", nullable: false),
                    snapshot_version = table.Column<decimal>(type: "numeric(20,0)", precision: 20, scale: 0, nullable: false),
                    state_source_date = table.Column<DateOnly>(type: "date", nullable: false),
                    state_loaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batch", x => x.batch_id);
                    table.CheckConstraint("ck_import_batch_hash", "octet_length(request_hash) = 32");
                    table.CheckConstraint("ck_import_batch_id", "batch_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_import_batch_result", "(outcome = 1 AND snapshot_version = expected_snapshot_version + 1 AND state_source_date = source_date) OR (outcome = 2 AND snapshot_version = expected_snapshot_version AND state_source_date = source_date) OR (outcome = 3 AND snapshot_version = expected_snapshot_version AND source_date < state_source_date)");
                    table.CheckConstraint("ck_import_batch_times", "source_date BETWEEN DATE '0001-01-01' AND DATE '9999-12-31' AND state_source_date BETWEEN DATE '0001-01-01' AND DATE '9999-12-31' AND isfinite(fetched_at) AND isfinite(state_loaded_at)");
                    table.CheckConstraint("ck_import_batch_versions", "expected_snapshot_version BETWEEN 0 AND 18446744073709551615 AND snapshot_version BETWEEN 1 AND 18446744073709551615");
                });

            migrationBuilder.CreateTable(
                name: "import_state",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    snapshot_version = table.Column<decimal>(type: "numeric(20,0)", precision: 20, scale: 0, nullable: false),
                    source_date = table.Column<DateOnly>(type: "date", nullable: true),
                    loaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_state", x => x.id);
                    table.CheckConstraint("ck_import_state_singleton", "id = 1");
                    table.CheckConstraint("ck_import_state_values", "(snapshot_version = 0 AND source_date IS NULL AND loaded_at IS NULL) OR (snapshot_version > 0 AND source_date IS NOT NULL AND source_date BETWEEN DATE '0001-01-01' AND DATE '9999-12-31' AND loaded_at IS NOT NULL AND isfinite(loaded_at))");
                    table.CheckConstraint("ck_import_state_version", "snapshot_version BETWEEN 0 AND 18446744073709551615");
                });

            migrationBuilder.CreateTable(
                name: "favorite",
                schema: "finance",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_favorite", x => new { x.user_id, x.currency_id });
                    table.CheckConstraint("ck_favorite_user_id", "user_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "FK_favorite_currency_currency_id",
                        column: x => x.currency_id,
                        principalSchema: "finance",
                        principalTable: "currency",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "finance",
                table: "import_state",
                columns: new[] { "id", "loaded_at", "snapshot_version", "source_date" },
                values: new object[] { 1, null, 0m, null });

            migrationBuilder.CreateIndex(
                name: "IX_currency_code",
                schema: "finance",
                table: "currency",
                column: "code",
                unique: true,
                filter: "is_available");

            migrationBuilder.CreateIndex(
                name: "IX_favorite_currency_id",
                schema: "finance",
                table: "favorite",
                column: "currency_id");
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE finance.currency, finance.favorite, finance.import_state, finance.import_batch TO finance_runtime;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "favorite",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "import_batch",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "import_state",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "currency",
                schema: "finance");
        }
    }
}
