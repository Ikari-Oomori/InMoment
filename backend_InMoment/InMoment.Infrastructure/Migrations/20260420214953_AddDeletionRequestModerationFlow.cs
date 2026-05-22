using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InMoment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletionRequestModerationFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProcessedByUserId",
                table: "account_deletion_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingNote",
                table: "account_deletion_requests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_deletion_requests_Status_RequestedAtUtc",
                table: "account_deletion_requests",
                columns: new[] { "Status", "RequestedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_account_deletion_requests_Status_RequestedAtUtc",
                table: "account_deletion_requests");

            migrationBuilder.DropColumn(
                name: "ProcessedByUserId",
                table: "account_deletion_requests");

            migrationBuilder.DropColumn(
                name: "ProcessingNote",
                table: "account_deletion_requests");
        }
    }
}
