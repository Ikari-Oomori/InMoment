using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InMoment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupConstraintsAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_group_members_GroupId_UserId",
                table: "group_members");

            migrationBuilder.DropIndex(
                name: "IX_group_invitations_GroupId_InvitedUserId_Status",
                table: "group_invitations");

            migrationBuilder.CreateIndex(
                name: "IX_group_members_GroupId_Owner_ActiveUnique",
                table: "group_members",
                column: "GroupId",
                unique: true,
                filter: "\"IsActive\" = true AND \"Role\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_group_members_GroupId_UserId_ActiveUnique",
                table: "group_members",
                columns: new[] { "GroupId", "UserId" },
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_group_invitations_GroupId_InvitedUserId_PendingUnique",
                table: "group_invitations",
                columns: new[] { "GroupId", "InvitedUserId" },
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_group_invitations_InvitedUserId_Status",
                table: "group_invitations",
                columns: new[] { "InvitedUserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_group_members_GroupId_Owner_ActiveUnique",
                table: "group_members");

            migrationBuilder.DropIndex(
                name: "IX_group_members_GroupId_UserId_ActiveUnique",
                table: "group_members");

            migrationBuilder.DropIndex(
                name: "IX_group_invitations_GroupId_InvitedUserId_PendingUnique",
                table: "group_invitations");

            migrationBuilder.DropIndex(
                name: "IX_group_invitations_InvitedUserId_Status",
                table: "group_invitations");

            migrationBuilder.CreateIndex(
                name: "IX_group_members_GroupId_UserId",
                table: "group_members",
                columns: new[] { "GroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_invitations_GroupId_InvitedUserId_Status",
                table: "group_invitations",
                columns: new[] { "GroupId", "InvitedUserId", "Status" });
        }
    }
}
