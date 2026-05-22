using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InMoment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentsCursorPaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_comments_PhotoId_CreatedAt_Id",
                table: "comments");

            migrationBuilder.CreateIndex(
                name: "IX_comments_ParentCommentId",
                table: "comments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_comments_PhotoId_CreatedAt_Id",
                table: "comments",
                columns: new[] { "PhotoId", "CreatedAt", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_comments_comments_ParentCommentId",
                table: "comments",
                column: "ParentCommentId",
                principalTable: "comments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_comments_comments_ParentCommentId",
                table: "comments");

            migrationBuilder.DropIndex(
                name: "IX_comments_ParentCommentId",
                table: "comments");

            migrationBuilder.DropIndex(
                name: "IX_comments_PhotoId_CreatedAt_Id",
                table: "comments");

            migrationBuilder.CreateIndex(
                name: "IX_comments_PhotoId_CreatedAt_Id",
                table: "comments",
                columns: new[] { "PhotoId", "CreatedAt" });
        }
    }
}
