using FluentAssertions;
using InMoment.API.Modules.Contacts;
using InMoment.Application.Features.Contacts.Common;
using InMoment.Application.Features.Contacts.Import;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InMoment.API.Tests.Modules.Contacts;

public sealed class ContactsControllerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private ContactsController Create()
        => new(_mediator.Object);

    [Fact]
    public async Task Import_ShouldMapRequest_AndReturnOk()
    {
        var resultDto = new ImportContactsResultDto(
            Matches: new List<ContactMatchDto>
            {
                new(
                    UserId: Guid.NewGuid(),
                    UserName: "visible_user",
                    FirstName: "Visible",
                    LastName: "User",
                    ProfilePhotoUrl: null,
                    MatchedBy: "email",
                    MatchedValue: "visible@test.com",
                    SourceContactDisplayName: "Visible Contact",
                    AlreadyFriend: false,
                    HasIncomingRequest: false,
                    HasOutgoingRequest: false,
                    CanSendFriendRequest: true)
            },
            Invites: new List<ContactInviteCandidateDto>
            {
                new(
                    DisplayName: "Unknown Contact",
                    Phone: "+49123456789",
                    Email: "unknown@test.com")
            });

        _mediator
            .Setup(x => x.Send(
                It.Is<ImportContactsCommand>(cmd =>
                    cmd.Contacts.Count == 2 &&
                    cmd.Contacts[0].DisplayName == "Visible Contact" &&
                    cmd.Contacts[0].Emails.SequenceEqual(new[] { "visible@test.com" }) &&
                    cmd.Contacts[1].DisplayName == "Unknown Contact" &&
                    cmd.Contacts[1].Phones.SequenceEqual(new[] { "+49 123 456 789" })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var controller = Create();

        var response = await controller.Import(
            new ImportContactsRequest(new[]
            {
                new ContactImportItemRequest(
                    "Visible Contact",
                    Array.Empty<string>(),
                    new[] { "visible@test.com" }),
                new ContactImportItemRequest(
                    "Unknown Contact",
                    new[] { "+49 123 456 789" },
                    new[] { "unknown@test.com" })
            }),
            CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ImportContactsResultDto>().Subject;

        payload.Matches.Should().ContainSingle();
        payload.Invites.Should().ContainSingle();

        _mediator.VerifyAll();
    }

    [Fact]
    public async Task Import_ShouldHandle_NullCollections()
    {
        var resultDto = new ImportContactsResultDto(
            Matches: Array.Empty<ContactMatchDto>(),
            Invites: Array.Empty<ContactInviteCandidateDto>());

        _mediator
            .Setup(x => x.Send(
                It.Is<ImportContactsCommand>(cmd =>
                    cmd.Contacts.Count == 1 &&
                    cmd.Contacts[0].DisplayName == "Contact" &&
                    cmd.Contacts[0].Phones.Count == 0 &&
                    cmd.Contacts[0].Emails.Count == 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var controller = Create();

        var response = await controller.Import(
            new ImportContactsRequest(new[]
            {
                new ContactImportItemRequest("Contact", null, null)
            }),
            CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ImportContactsResultDto>().Subject;

        payload.Matches.Should().BeEmpty();
        payload.Invites.Should().BeEmpty();

        _mediator.VerifyAll();
    }
}