using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InMoment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivacyAndBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blocked_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blocked_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "privacy_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowFriendRequestsFrom = table.Column<int>(type: "integer", nullable: false),
                    AllowGroupInvitesFrom = table.Column<int>(type: "integer", nullable: false),
                    DiscoverableByContacts = table.Column<bool>(type: "boolean", nullable: false),
                    DiscoverableBySearch = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_privacy_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_blocked_users_BlockedUserId",
                table: "blocked_users",
                column: "BlockedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_blocked_users_UserId",
                table: "blocked_users",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_blocked_users_UserId_BlockedUserId",
                table: "blocked_users",
                columns: new[] { "UserId", "BlockedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_privacy_settings_UserId",
                table: "privacy_settings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blocked_users");

            migrationBuilder.DropTable(
                name: "privacy_settings");
        }
    }
}
