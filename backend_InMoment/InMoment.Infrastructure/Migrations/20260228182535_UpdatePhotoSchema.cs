using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InMoment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePhotoSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_photos_GroupId_CreatedAt",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "PublicUrl",
                table: "photos");

            migrationBuilder.AddColumn<string>(
                 name: "ContentType",
                 table: "photos",
                 type: "character varying(100)",
                 maxLength: 100,
                 nullable: false,
                 defaultValue: "image/jpeg");

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "photos",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateIndex(
                name: "IX_photos_GroupId_CreatedAt_Id_Active",
                table: "photos",
                columns: new[] { "GroupId", "CreatedAt", "Id" },
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_photos_GroupId_CreatedAt_Id_Active",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "photos");

            migrationBuilder.AddColumn<string>(
                name: "PublicUrl",
                table: "photos",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_photos_GroupId_CreatedAt",
                table: "photos",
                columns: new[] { "GroupId", "CreatedAt" });
        }
    }
}
