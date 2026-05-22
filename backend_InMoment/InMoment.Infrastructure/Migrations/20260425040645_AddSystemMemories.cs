using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InMoment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemMemories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SystemMemoryId",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "system_memories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Subtitle = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    SourcePhotoIds = table.Column<string>(type: "text", nullable: false),
                    PreviewPhotoId = table.Column<Guid>(type: "uuid", nullable: true),
                    GeneratedVideoStorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    GeneratedVideoContentType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    GeneratedVideoSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    PeriodStartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ViewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_memories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_system_memories_UserId_CreatedAtUtc",
                table: "system_memories",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_system_memories_UserId_Period_PeriodEndedAtUtc",
                table: "system_memories",
                columns: new[] { "UserId", "Period", "PeriodEndedAtUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_memories");

            migrationBuilder.DropColumn(
                name: "SystemMemoryId",
                table: "notifications");
        }
    }
}
