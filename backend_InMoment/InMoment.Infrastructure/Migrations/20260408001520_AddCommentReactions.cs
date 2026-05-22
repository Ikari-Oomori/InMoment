using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InMoment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentReactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "comment_reactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comment_reactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_comment_reactions_CommentId",
                table: "comment_reactions",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_comment_reactions_CommentId_UserId",
                table: "comment_reactions",
                columns: new[] { "CommentId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comment_reactions");
        }
    }
}
