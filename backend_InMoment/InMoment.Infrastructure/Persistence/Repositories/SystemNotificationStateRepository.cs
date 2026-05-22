using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class SystemNotificationStateRepository : ISystemNotificationStateRepository
{
    private readonly AppDbContext _db;

    public SystemNotificationStateRepository(AppDbContext db) => _db = db;

    public Task<SystemNotificationState?> GetByUserIdAsync(Guid userId, CancellationToken ct)
        => _db.SystemNotificationStates.FirstOrDefaultAsync(x => x.UserId == userId, ct);

    public Task AddAsync(SystemNotificationState state, CancellationToken ct)
        => _db.SystemNotificationStates.AddAsync(state, ct).AsTask();
}