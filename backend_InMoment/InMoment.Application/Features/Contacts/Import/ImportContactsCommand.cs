using InMoment.Application.Features.Contacts.Common;
using MediatR;

namespace InMoment.Application.Features.Contacts.Import;

public sealed record ImportContactsCommand(
    IReadOnlyList<ContactImportItemDto> Contacts
) : IRequest<ImportContactsResultDto>;