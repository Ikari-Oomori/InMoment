using FluentAssertions;
using InMoment.API.Modules.Privacy;
using InMoment.Application.Features.Privacy.Common;
using InMoment.Application.Features.Privacy.GetPrivacy;
using InMoment.Application.Features.Privacy.UpdatePrivacy;
using InMoment.Domain.Privacy;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InMoment.Tests.IntegrationTests.Privacy;

public sealed class PrivacyControllerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private PrivacyController Create()
        => new(_mediator.Object);

    [Fact]
    public async Task Get_ShouldSendQuery_AndReturnOk()
    {
        var dto = new PrivacySettingsDto(
            PrivacyAudience.Everyone,
            PrivacyAudience.FriendsOnly,
            true,
            false);

        _mediator.Setup(x => x.Send(It.IsAny<GetPrivacyQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var controller = Create();

        var result = await controller.Get(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(dto);

        _mediator.Verify(
            x => x.Send(It.Is<GetPrivacyQuery>(_ => true), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Update_ShouldSendCommand_AndReturnNoContent()
    {
        var controller = Create();

        var request = new UpdatePrivacyRequest(
            PrivacyAudience.FriendsOnly,
            PrivacyAudience.Nobody,
            false,
            true);

        var result = await controller.Update(request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        _mediator.Verify(
            x => x.Send(
                It.Is<UpdatePrivacyCommand>(c =>
                    c.AllowFriendRequestsFrom == PrivacyAudience.FriendsOnly &&
                    c.AllowGroupInvitesFrom == PrivacyAudience.Nobody &&
                    c.DiscoverableByContacts == false &&
                    c.DiscoverableBySearch == true),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}