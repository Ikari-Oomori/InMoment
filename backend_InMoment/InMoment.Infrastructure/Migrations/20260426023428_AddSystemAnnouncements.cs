using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InMoment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SystemAnnouncementId",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "system_announcements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    MediaUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MediaContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_announcements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_SystemAnnouncementId",
                table: "notifications",
                column: "SystemAnnouncementId");

            migrationBuilder.CreateIndex(
                name: "IX_system_announcements_CreatedAtUtc_Id",
                table: "system_announcements",
                columns: new[] { "CreatedAtUtc", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_system_announcements_SystemAnnouncementId",
                table: "notifications",
                column: "SystemAnnouncementId",
                principalTable: "system_announcements",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notifications_system_announcements_SystemAnnouncementId",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_SystemAnnouncementId",
                table: "notifications");

            migrationBuilder.DropTable(
                name: "system_announcements");

            migrationBuilder.DropColumn(
                name: "SystemAnnouncementId",
                table: "notifications");
        }
    }
}