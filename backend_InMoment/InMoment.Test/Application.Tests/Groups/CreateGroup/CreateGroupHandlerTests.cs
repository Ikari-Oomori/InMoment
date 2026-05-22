using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Groups.CreateGroup;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;

namespace InMoment.Application.Tests.Groups.CreateGroup;

public sealed class CreateGroupHandlerTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();

    private CreateGroupHandler Create()
        => new(
            _groups.Object,
            _uow.Object,
            _current.Object);

    [Fact]
    public async Task Handle_ShouldCreateGroup_AndReturnResult()
    {
        var currentUserId = Guid.NewGuid();
        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        Group? addedGroup = null;

        _groups.Setup(x => x.AddAsync(It.IsAny<Group>(), It.IsAny<CancellationToken>()))
            .Callback<Group, CancellationToken>((group, _) => addedGroup = group)
            .Returns(Task.CompletedTask);

        var handler = Create();

        var result = await handler.Handle(
            new CreateGroupCommand("  My Test Group  "),
            CancellationToken.None);

        result.GroupId.Should().NotBe(Guid.Empty);

        addedGroup.Should().NotBeNull();
        addedGroup!.Id.Should().Be(result.GroupId);
        addedGroup.Name.Should().Be("My Test Group");
        addedGroup.OwnerId.Should().Be(currentUserId);
        addedGroup.IsActive.Should().BeTrue();

        addedGroup.Members.Should().HaveCount(1);
        addedGroup.Members.Should().ContainSingle(x =>
            x.UserId == currentUserId &&
            x.Role == GroupRole.Owner &&
            x.IsActive);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task Handle_ShouldThrowValidationException_WhenNameIsEmpty(string name)
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.NewGuid());

        var handler = Create();

        var act = () => handler.Handle(new CreateGroupCommand(name), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Group name is required.");

        _groups.Verify(x => x.AddAsync(It.IsAny<Group>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}