using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InMoment.Infrastructure.Migrations
{
    public partial class BackfillRetentionAndProductUpdateFlags : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE notification_settings
                SET "PushRetention" = TRUE
                WHERE "PushRetention" = FALSE;
            """);

            migrationBuilder.Sql("""
                UPDATE notification_settings
                SET "PushProductUpdates" = TRUE
                WHERE "PushProductUpdates" = FALSE;
            """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE notification_settings
                SET "PushRetention" = FALSE
                WHERE "PushRetention" = TRUE;
            """);

            migrationBuilder.Sql("""
                UPDATE notification_settings
                SET "PushProductUpdates" = FALSE
                WHERE "PushProductUpdates" = TRUE;
            """);
        }
    }
}