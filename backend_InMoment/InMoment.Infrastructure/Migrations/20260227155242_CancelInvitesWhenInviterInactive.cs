using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InMoment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CancelInvitesWhenInviterInactive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.cancel_pending_invites_when_member_inactive()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    -- интересует только переход true -> false
    IF (TG_OP = 'UPDATE')
       AND (OLD.""IsActive"" = true)
       AND (NEW.""IsActive"" = false) THEN

        UPDATE public.group_invitations
        SET ""Status"" = 4,          -- Cancelled
            ""RespondedAt"" = now()
        WHERE ""GroupId"" = NEW.""GroupId""
          AND ""InvitedByUserId"" = NEW.""UserId""
          AND ""Status"" = 1;        -- Pending
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_cancel_pending_invites_when_member_inactive ON public.group_members;

CREATE TRIGGER trg_cancel_pending_invites_when_member_inactive
AFTER UPDATE OF ""IsActive"" ON public.group_members
FOR EACH ROW
EXECUTE FUNCTION public.cancel_pending_invites_when_member_inactive();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_cancel_pending_invites_when_member_inactive ON public.group_members;
DROP FUNCTION IF EXISTS public.cancel_pending_invites_when_member_inactive();
");

        }
    }
}
