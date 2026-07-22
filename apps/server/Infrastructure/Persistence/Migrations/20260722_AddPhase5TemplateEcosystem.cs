using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankersSeat.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase5TemplateEcosystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Template Shares table
            migrationBuilder.CreateTable(
                name: "template_shares",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    template_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    shared_by_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    shared_with_email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    granted_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    revoked_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_template_shares", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_template_shares_template_id_shared_with_email",
                table: "template_shares",
                columns: new[] { "template_id", "shared_with_email" });

            migrationBuilder.CreateIndex(
                name: "ix_template_shares_shared_by_user_id",
                table: "template_shares",
                column: "shared_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_template_shares_shared_with_email",
                table: "template_shares",
                column: "shared_with_email");

            migrationBuilder.CreateIndex(
                name: "ix_template_shares_granted_at_utc",
                table: "template_shares",
                column: "granted_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_template_shares_active",
                table: "template_shares",
                columns: new[] { "template_id", "shared_with_email", "revoked_at_utc" },
                unique: true,
                filter: "revoked_at_utc IS NULL");

            // Template Metadata table
            migrationBuilder.CreateTable(
                name: "template_metadata",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    template_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    edition_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    owner_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    author = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    author_email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    author_url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    license = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    published_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    template_status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    moderation_status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    download_count = table.Column<int>(type: "INTEGER", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    flag_reasons = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_template_metadata", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_template_metadata_template_id_edition_id",
                table: "template_metadata",
                columns: new[] { "template_id", "edition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_template_metadata_owner_user_id",
                table: "template_metadata",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_template_metadata_template_status",
                table: "template_metadata",
                column: "template_status");

            migrationBuilder.CreateIndex(
                name: "ix_template_metadata_moderation_status",
                table: "template_metadata",
                column: "moderation_status");

            migrationBuilder.CreateIndex(
                name: "ix_template_metadata_updated_at_utc",
                table: "template_metadata",
                column: "updated_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "template_shares");
            migrationBuilder.DropTable(name: "template_metadata");
        }
    }
}
