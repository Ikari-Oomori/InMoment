using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Contacts;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class ContactImportLogRepository : IContactImportLogRepository
{
    private readonly AppDbContext _db;

    public ContactImportLogRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(ContactImportLog log, CancellationToken ct)
        => _db.Set<ContactImportLog>().AddAsync(log, ct).AsTask();
}