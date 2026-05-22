using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InMoment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedIdentityColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeletedEmail",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedUserName",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_DeletedEmail",
                table: "users",
                column: "DeletedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_users_DeletedUserName",
                table: "users",
                column: "DeletedUserName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_DeletedEmail",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_DeletedUserName",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DeletedEmail",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DeletedUserName",
                table: "users");
        }
    }
}
