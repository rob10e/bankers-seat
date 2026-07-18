using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankersSeat.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerFieldValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_field_values",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FieldId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ValueJson = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_field_values", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_field_values_SessionId_ParticipantId",
                table: "player_field_values",
                columns: new[] { "SessionId", "ParticipantId" });

            migrationBuilder.CreateIndex(
                name: "IX_player_field_values_SessionId_ParticipantId_FieldId",
                table: "player_field_values",
                columns: new[] { "SessionId", "ParticipantId", "FieldId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_field_values");
        }
    }
}
