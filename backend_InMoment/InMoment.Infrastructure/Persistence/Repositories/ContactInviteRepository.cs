using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class ContactInviteRepository : IContactInviteRepository
{
    private readonly AppDbContext _db;

    public ContactInviteRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(ContactInvite invite, CancellationToken ct)
        => _db.Set<ContactInvite>().AddAsync(invite, ct).AsTask();

    public Task<ContactInvite?> GetByIdAsync(Guid inviteId, CancellationToken ct)
        => _db.Set<ContactInvite>().FirstOrDefaultAsync(x => x.Id == inviteId, ct);

    public async Task<IReadOnlyList<ContactInvite>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        return await _db.Set<ContactInvite>()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public Task<ContactInvite?> GetPendingByEmailAsync(Guid userId, string email, CancellationToken ct)
        => _db.Set<ContactInvite>().FirstOrDefaultAsync(x =>
            x.UserId == userId &&
            x.Email == email &&
            x.Status == ContactInviteStatus.Pending, ct);

    public Task<ContactInvite?> GetPendingByPhoneAsync(Guid userId, string phoneNumber, CancellationToken ct)
        => _db.Set<ContactInvite>().FirstOrDefaultAsync(x =>
            x.UserId == userId &&
            x.PhoneNumber == phoneNumber &&
            x.Status == ContactInviteStatus.Pending, ct);
}