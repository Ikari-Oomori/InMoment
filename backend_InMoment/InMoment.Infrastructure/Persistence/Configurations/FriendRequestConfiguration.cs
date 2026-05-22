using InMoment.Domain.Friends;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class FriendRequestConfiguration : IEntityTypeConfiguration<FriendRequest>
{
    public void Configure(EntityTypeBuilder<FriendRequest> b)
    {
        b.ToTable("friend_requests");

        b.HasKey(x => x.Id);

        b.Property(x => x.FromUserId).IsRequired();
        b.Property(x => x.ToUserId).IsRequired();
        b.Property(x => x.Status).IsRequired();

        b.HasIndex(x => new { x.ToUserId, x.Status });
        b.HasIndex(x => new { x.FromUserId, x.Status });

        b.HasIndex(x => new { x.FromUserId, x.ToUserId, x.Status })
            .HasDatabaseName("IX_friend_requests_from_to_status");
    }
}