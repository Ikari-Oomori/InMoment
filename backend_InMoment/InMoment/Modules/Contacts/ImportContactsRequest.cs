namespace InMoment.API.Modules.Contacts;

public sealed record ImportContactsRequest(
    IReadOnlyList<ContactImportItemRequest> Contacts);

public sealed record ContactImportItemRequest(
    string? DisplayName,
    IReadOnlyList<string>? Phones,
    IReadOnlyList<string>? Emails);