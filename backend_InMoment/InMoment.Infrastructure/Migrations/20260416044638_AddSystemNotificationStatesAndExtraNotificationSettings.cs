using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InMoment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemNotificationStatesAndExtraNotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PushProductUpdates",
                table: "notification_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PushRetention",
                table: "notification_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "system_notification_states",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastShareReminderSentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFeedbackPromptSentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAnniversaryYearSent = table.Column<int>(type: "integer", nullable: true),
                    LastProductAnnouncementKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_notification_states", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_system_notification_states_UserId",
                table: "system_notification_states",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_notification_states");

            migrationBuilder.DropColumn(
                name: "PushProductUpdates",
                table: "notification_settings");

            migrationBuilder.DropColumn(
                name: "PushRetention",
                table: "notification_settings");
        }
    }
}
