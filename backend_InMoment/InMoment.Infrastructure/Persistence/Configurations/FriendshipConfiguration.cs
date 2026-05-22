using InMoment.Domain.Friends;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> b)
    {
        b.ToTable("friendships");

        b.HasKey(x => x.Id);

        b.Property(x => x.User1Id).IsRequired();
        b.Property(x => x.User2Id).IsRequired();

        b.HasIndex(x => new { x.User1Id, x.User2Id })
            .IsUnique();
    }
}