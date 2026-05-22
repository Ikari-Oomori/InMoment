using FluentAssertions;
using InMoment.API.Modules.Friends;
using InMoment.Application.Features.Friends.Suggestions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InMoment.Tests.API.Tests.Friends;

public sealed class FriendsSuggestionsControllerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private FriendSuggestionsController Create()
        => new(_mediator.Object);

    [Fact]
    public async Task Get_ShouldReturnOk_WithSuggestions()
    {
        var expected = new[]
        {
            new FriendSuggestionDto(
                Guid.NewGuid(),
                "user1",
                "User",
                "One",
                null,
                false,
                false,
                false),
            new FriendSuggestionDto(
                Guid.NewGuid(),
                "user2",
                "User",
                "Two",
                "https://cdn.example.com/u2.jpg",
                true,
                false,
                true)
        };

        _mediator.Setup(x => x.Send(
                It.Is<SearchFriendSuggestionsQuery>(q => q.Query == "ann" && q.Limit == 10),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.Get("ann", 10, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Get_ShouldReturnOk_WithEmptyList_WhenNoSuggestions()
    {
        var expected = Array.Empty<FriendSuggestionDto>();

        _mediator.Setup(x => x.Send(
                It.Is<SearchFriendSuggestionsQuery>(q => q.Query == "ann" && q.Limit == 10),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.Get("ann", 10, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }
}