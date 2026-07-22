using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankersSeat.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompletePendingMigrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_template_shares_TemplateId_SharedWithEmail_RevokedAtUtc",
                table: "template_shares",
                columns: new[] { "TemplateId", "SharedWithEmail", "RevokedAtUtc" },
                unique: true,
                filter: "revoked_at_utc IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_template_shares_TemplateId_SharedWithEmail_RevokedAtUtc",
                table: "template_shares");
        }
    }
}
