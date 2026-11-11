using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CurrencyRates.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "user",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    password_hash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user", x => x.id);
                    table.CheckConstraint("ck_user_id", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_user_name", "name COLLATE \"C\" ~ '^[A-Za-z0-9_.-]{3,50}$'");
                    table.CheckConstraint("ck_user_normalized_name", "normalized_name = upper(name COLLATE \"C\")");
                    table.CheckConstraint("ck_user_password_hash", "length(btrim(password_hash)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "session",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session", x => x.id);
                    table.CheckConstraint("ck_session_id", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_session_times", "isfinite(created_at) AND isfinite(expires_at) AND expires_at > created_at AND (revoked_at IS NULL OR (isfinite(revoked_at) AND revoked_at >= created_at))");
                    table.ForeignKey(
                        name: "FK_session_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_user_id",
                schema: "identity",
                table: "session",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_normalized_name",
                schema: "identity",
                table: "user",
                column: "normalized_name",
                unique: true);
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE identity.\"user\", identity.session TO identity_runtime;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user",
                schema: "identity");
        }
    }
}
