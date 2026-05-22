using InMoment.Domain.Contacts;
using InMoment.Domain.Friends;
using InMoment.Domain.Groups;
using InMoment.Domain.Media;
using InMoment.Domain.Notifications;
using InMoment.Domain.Privacy;
using InMoment.Domain.Reports;
using InMoment.Domain.Security;
using InMoment.Domain.SystemAnnouncements;
using InMoment.Domain.SystemMemories;
using InMoment.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AccountDeletionRequest> AccountDeletionRequests => Set<AccountDeletionRequest>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<GroupInvitation> GroupInvitations => Set<GroupInvitation>();
    public DbSet<GroupInviteCode> GroupInviteCodes => Set<GroupInviteCode>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Reaction> Reactions => Set<Reaction>();
    public DbSet<SavedPhoto> SavedPhotos => Set<SavedPhoto>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationSettings> NotificationSettings => Set<NotificationSettings>();
    public DbSet<SystemNotificationState> SystemNotificationStates => Set<SystemNotificationState>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<ContactImportLog> ContactImportLogs => Set<ContactImportLog>();
    public DbSet<ContactInvite> ContactInvites => Set<ContactInvite>();
    public DbSet<PrivacySettings> PrivacySettings => Set<PrivacySettings>();
    public DbSet<BlockedUser> BlockedUsers => Set<BlockedUser>();
    public DbSet<CommentReaction> CommentReactions => Set<CommentReaction>();
    public DbSet<SystemMemory> SystemMemories => Set<SystemMemory>();
    public DbSet<SystemAnnouncement> SystemAnnouncements => Set<SystemAnnouncement>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}