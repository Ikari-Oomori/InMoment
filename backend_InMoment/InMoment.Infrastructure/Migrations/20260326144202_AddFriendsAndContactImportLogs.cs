using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InMoment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFriendsAndContactImportLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contact_import_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactsSubmitted = table.Column<int>(type: "integer", nullable: false),
                    MatchesFound = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_import_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "friend_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_friend_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "friendships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    User1Id = table.Column<Guid>(type: "uuid", nullable: false),
                    User2Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_friendships", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contact_import_logs_CreatedAtUtc",
                table: "contact_import_logs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_contact_import_logs_UserId",
                table: "contact_import_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_friend_requests_from_to_status",
                table: "friend_requests",
                columns: new[] { "FromUserId", "ToUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_friend_requests_FromUserId_Status",
                table: "friend_requests",
                columns: new[] { "FromUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_friend_requests_ToUserId_Status",
                table: "friend_requests",
                columns: new[] { "ToUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_friendships_User1Id_User2Id",
                table: "friendships",
                columns: new[] { "User1Id", "User2Id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contact_import_logs");

            migrationBuilder.DropTable(
                name: "friend_requests");

            migrationBuilder.DropTable(
                name: "friendships");
        }
    }
}
