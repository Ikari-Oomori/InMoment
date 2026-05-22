using InMoment.Domain.Contacts;

namespace InMoment.Application.Abstractions.Persistence;

public interface IContactImportLogRepository
{
    Task AddAsync(ContactImportLog log, CancellationToken ct);
}