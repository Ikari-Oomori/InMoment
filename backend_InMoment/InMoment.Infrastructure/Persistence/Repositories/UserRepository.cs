using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public Task AddAsync(User user, CancellationToken ct)
        => _db.Users.AddAsync(user, ct).AsTask();

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct)
        => _db.Users.FirstOrDefaultAsync(x => x.Email == email, ct);

    public Task<User?> GetByUserNameAsync(string userName, CancellationToken ct)
        => _db.Users.FirstOrDefaultAsync(x => x.UserName == userName, ct);

    public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken ct)
        => _db.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber, ct);

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct)
        => _db.Users.AnyAsync(x => x.Email == email, ct);

    public Task<bool> UserNameExistsAsync(string userName, CancellationToken ct)
        => _db.Users.AnyAsync(x => x.UserName == userName, ct);

    public Task<bool> PhoneNumberExistsAsync(string phoneNumber, CancellationToken ct)
        => _db.Users.AnyAsync(x => x.PhoneNumber == phoneNumber, ct);

    public async Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return Array.Empty<User>();

        return await _db.Users
            .Where(u => ids.Contains(u.Id))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<User>> SearchAsync(
        string query,
        int limit,
        Guid currentUserId,
        CancellationToken ct)
    {
        query = (query ?? string.Empty).Trim();
        if (query.Length == 0)
            return Array.Empty<User>();

        var normalized = query.ToLowerInvariant();

        return await _db.Users
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.Id != currentUserId &&
                (
                    x.UserName.ToLower().Contains(normalized) ||
                    x.FirstName.ToLower().Contains(normalized) ||
                    x.LastName.ToLower().Contains(normalized)
                ))
            .OrderBy(x => x.UserName)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<User>> SearchByPrefixAsync(
        string prefix,
        int limit,
        Guid currentUserId,
        CancellationToken ct)
    {
        prefix = (prefix ?? string.Empty).Trim();
        if (prefix.Length == 0)
            return Array.Empty<User>();

        var normalized = prefix.ToLowerInvariant();

        return await _db.Users
            .AsNoTracking()
            .Where(u =>
                u.IsActive &&
                u.Id != currentUserId &&
                u.UserName.ToLower().StartsWith(normalized))
            .OrderBy(u => u.UserName)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<User>> GetByEmailsAsync(
        IReadOnlyCollection<string> emails,
        CancellationToken ct)
    {
        if (emails.Count == 0)
            return Array.Empty<User>();

        var normalized = emails
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (normalized.Count == 0)
            return Array.Empty<User>();

        return await _db.Users
            .Where(x => x.IsActive && normalized.Contains(x.Email))
            .ToListAsync(ct);
    }

    public async Task<User?> GetByDeletedEmailAsync(string email, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();

        return await _db.Users
            .FirstOrDefaultAsync(x => x.DeletedEmail == normalized, ct);
    }

    public async Task<User?> GetByDeletedUserNameAsync(string userName, CancellationToken ct)
    {
        var normalized = userName.Trim();

        return await _db.Users
            .FirstOrDefaultAsync(x => x.DeletedUserName == normalized, ct);
    }

    public async Task<IReadOnlyList<Guid>> GetActiveUserIdsAsync(CancellationToken ct)
    {
        return await _db.Users
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(ct);
    }
}