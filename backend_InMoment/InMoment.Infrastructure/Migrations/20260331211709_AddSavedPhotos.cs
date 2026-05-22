using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InMoment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saved_photos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_photos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_saved_photos_PhotoId",
                table: "saved_photos",
                column: "PhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_saved_photos_PhotoId_UserId",
                table: "saved_photos",
                columns: new[] { "PhotoId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saved_photos_UserId_CreatedAt_Id",
                table: "saved_photos",
                columns: new[] { "UserId", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_photos");
        }
    }
}
