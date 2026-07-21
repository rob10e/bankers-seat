using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankersSeat.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase4Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    password_hash_bcrypt = table.Column<string>(type: "TEXT", nullable: false),
                    display_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_authenticated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    token_hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_revoked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    session_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    actor_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    actor_participant_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    action = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    details = table.Column<string>(type: "TEXT", nullable: false),
                    ip_address = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "TEXT", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    result = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "session_metadata",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_accessed_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    participant_count = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session_metadata", x => x.session_id);
                });

            migrationBuilder.CreateTable(
                name: "join_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    session_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    link_token = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    use_count = table.Column<int>(type: "INTEGER", nullable: false),
                    is_revoked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_join_links", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "session_ttl_policies",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    retention_days = table.Column<int>(type: "INTEGER", nullable: false),
                    auto_delete_on_complete = table.Column<bool>(type: "INTEGER", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_archived = table.Column<bool>(type: "INTEGER", nullable: false),
                    archived_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session_ttl_policies", x => x.session_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_accounts_email",
                table: "user_accounts",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_accounts_created_at_utc",
                table: "user_accounts",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id_expires_at_utc",
                table: "refresh_tokens",
                columns: new[] { "user_id", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_created_at_utc",
                table: "refresh_tokens",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_session_id",
                table: "audit_logs",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_user_id",
                table: "audit_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_created_at_utc",
                table: "audit_logs",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_session_id_created_at_utc",
                table: "audit_logs",
                columns: new[] { "session_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_session_metadata_owner_user_id",
                table: "session_metadata",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_metadata_last_accessed_at_utc",
                table: "session_metadata",
                column: "last_accessed_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_join_links_link_token",
                table: "join_links",
                column: "link_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_join_links_session_id_expires_at_utc",
                table: "join_links",
                columns: new[] { "session_id", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_session_ttl_policies_expires_at_utc",
                table: "session_ttl_policies",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_session_ttl_policies_is_archived",
                table: "session_ttl_policies",
                column: "is_archived");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "user_accounts");
            migrationBuilder.DropTable(name: "refresh_tokens");
            migrationBuilder.DropTable(name: "audit_logs");
            migrationBuilder.DropTable(name: "session_metadata");
            migrationBuilder.DropTable(name: "join_links");
            migrationBuilder.DropTable(name: "session_ttl_policies");
        }
    }
}
