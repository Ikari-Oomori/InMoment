using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InMoment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExtendRefreshSessionsWithGeo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GeoCity",
                table: "refresh_sessions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeoCountry",
                table: "refresh_sessions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeoProvider",
                table: "refresh_sessions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeoRegion",
                table: "refresh_sessions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GeoCity",
                table: "refresh_sessions");

            migrationBuilder.DropColumn(
                name: "GeoCountry",
                table: "refresh_sessions");

            migrationBuilder.DropColumn(
                name: "GeoProvider",
                table: "refresh_sessions");

            migrationBuilder.DropColumn(
                name: "GeoRegion",
                table: "refresh_sessions");
        }
    }
}
