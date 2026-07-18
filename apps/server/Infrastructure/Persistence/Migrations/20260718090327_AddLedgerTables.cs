using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankersSeat.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ledger_postings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TransactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<long>(type: "INTEGER", nullable: false),
                    BalanceAfter = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_postings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ledger_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    ActorParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CorrectsTransactionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_transactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_postings_SessionId_AccountId",
                table: "ledger_postings",
                columns: new[] { "SessionId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_postings_TransactionId_Id",
                table: "ledger_postings",
                columns: new[] { "TransactionId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_transactions_SessionId_CorrectsTransactionId",
                table: "ledger_transactions",
                columns: new[] { "SessionId", "CorrectsTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_transactions_SessionId_CreatedAtUtc",
                table: "ledger_transactions",
                columns: new[] { "SessionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_transactions_SessionId_Sequence",
                table: "ledger_transactions",
                columns: new[] { "SessionId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ledger_postings");

            migrationBuilder.DropTable(
                name: "ledger_transactions");
        }
    }
}
