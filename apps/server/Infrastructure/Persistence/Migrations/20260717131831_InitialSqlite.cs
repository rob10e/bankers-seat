using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankersSeat.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Balance = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "game_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoomCode = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    HostParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TemplateSnapshotId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActorParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    CommandType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    RequestHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ResultHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IdentityKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    JoinOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ReconnectSecretHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "template_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TemplateId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EditionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TemplateVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SchemaVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    TemplateJson = table.Column<string>(type: "TEXT", nullable: false),
                    StartingPlayerBalance = table.Column<long>(type: "INTEGER", nullable: false),
                    AllowPlayerOverdraft = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_template_snapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_SessionId_OwnerId_OwnerType",
                table: "accounts",
                columns: new[] { "SessionId", "OwnerId", "OwnerType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_game_sessions_CreatedAtUtc",
                table: "game_sessions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_game_sessions_Id_SessionVersion",
                table: "game_sessions",
                columns: new[] { "Id", "SessionVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_game_sessions_RoomCode",
                table: "game_sessions",
                column: "RoomCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_SessionId_ActorParticipantId_Key",
                table: "idempotency_records",
                columns: new[] { "SessionId", "ActorParticipantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_participants_SessionId_Id",
                table: "participants",
                columns: new[] { "SessionId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_participants_SessionId_JoinOrder",
                table: "participants",
                columns: new[] { "SessionId", "JoinOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_template_snapshots_TemplateId_EditionId_TemplateVersion",
                table: "template_snapshots",
                columns: new[] { "TemplateId", "EditionId", "TemplateVersion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "game_sessions");

            migrationBuilder.DropTable(
                name: "idempotency_records");

            migrationBuilder.DropTable(
                name: "participants");

            migrationBuilder.DropTable(
                name: "template_snapshots");
        }
    }
}
