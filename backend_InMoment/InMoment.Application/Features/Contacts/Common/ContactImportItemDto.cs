namespace InMoment.Application.Features.Contacts.Common;

public sealed record ContactImportItemDto(
    string? DisplayName,
    IReadOnlyList<string> Phones,
    IReadOnlyList<string> Emails
);