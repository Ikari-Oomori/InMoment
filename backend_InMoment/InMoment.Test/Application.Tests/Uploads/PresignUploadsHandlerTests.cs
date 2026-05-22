using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Application.Features.Uploads.PresignGroupAvatarUpload;
using InMoment.Application.Features.Uploads.PresignPhotoUpload;
using InMoment.Application.Features.Uploads.PresignProfilePhotoUpload;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;

namespace InMoment.Application.Tests.Uploads;

public sealed class PresignUploadHandlersTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IFileStorage> _storage = new();

    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();

    private PresignGroupAvatarUploadHandler CreateGroupAvatarHandler()
        => new(_groups.Object, _current.Object, _storage.Object);

    private PresignPhotoUploadHandler CreatePhotoHandler()
        => new(_current.Object, _groups.Object, _storage.Object);

    private PresignProfilePhotoUploadHandler CreateProfilePhotoHandler()
        => new(_current.Object, _storage.Object);

    private static PresignedUploadResult CreatePresignResult(string key, string contentType)
        => new(
            UploadUrl: $"https://upload.example.com/{Uri.EscapeDataString(key)}",
            Key: key,
            FileUrl: $"https://cdn.example.com/{key}",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(10));

    [Fact]
    public async Task PresignGroupAvatar_ShouldThrow_WhenGroupIdIsEmpty()
    {
        _current.Setup(x => x.UserId).Returns(_currentUserId);

        var handler = CreateGroupAvatarHandler();

        var act = () => handler.Handle(
            new PresignGroupAvatarUploadCommand(Guid.Empty, "image/jpeg"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("GroupId is required.");

        _storage.Verify(x => x.GetPresignedUploadUrlAsync(
            It.IsAny<PresignedUploadRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PresignGroupAvatar_ShouldThrow_WhenGroupNotFound()
    {
        _current.Setup(x => x.UserId).Returns(_currentUserId);

        _groups.Setup(x => x.GetByIdAsync(_groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = CreateGroupAvatarHandler();

        var act = () => handler.Handle(
            new PresignGroupAvatarUploadCommand(_groupId, "image/jpeg"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");
    }

    [Fact]
    public async Task PresignGroupAvatar_ShouldThrow_WhenUserIsNotOwner()
    {
        var ownerId = Guid.NewGuid();
        var group = Group.Create("Test Group", ownerId);
        group.AddMember(_currentUserId);

        _current.Setup(x => x.UserId).Returns(_currentUserId);

        _groups.Setup(x => x.GetByIdAsync(_groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = CreateGroupAvatarHandler();

        var act = () => handler.Handle(
            new PresignGroupAvatarUploadCommand(_groupId, "image/jpeg"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();

        _storage.Verify(x => x.GetPresignedUploadUrlAsync(
            It.IsAny<PresignedUploadRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("application/pdf")]
    [InlineData("video/mp4")]
    public async Task PresignGroupAvatar_ShouldThrow_WhenContentTypeUnsupported(string contentType)
    {
        var group = Group.Create("Test Group", _currentUserId);

        _current.Setup(x => x.UserId).Returns(_currentUserId);

        _groups.Setup(x => x.GetByIdAsync(_groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = CreateGroupAvatarHandler();

        var act = () => handler.Handle(
            new PresignGroupAvatarUploadCommand(_groupId, contentType),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Unsupported content type.");
    }

    [Fact]
    public async Task PresignGroupAvatar_ShouldReturnPresign_ForOwner()
    {
        var group = Group.Create("Test Group", _currentUserId);

        _current.Setup(x => x.UserId).Returns(_currentUserId);

        _groups.Setup(x => x.GetByIdAsync(_groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _storage.Setup(x => x.GetPresignedUploadUrlAsync(
                It.IsAny<PresignedUploadRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PresignedUploadRequest req, CancellationToken _) => CreatePresignResult(req.Key, req.ContentType));

        var handler = CreateGroupAvatarHandler();

        var result = await handler.Handle(
            new PresignGroupAvatarUploadCommand(_groupId, "image/png"),
            CancellationToken.None);

        result.UploadUrl.Should().NotBeNullOrWhiteSpace();
        result.StorageKey.Should().Contain($".png");
        result.StorageKey.Should().Contain("groups/");
        result.FileUrl.Should().Contain(result.StorageKey);
    }

    [Fact]
    public async Task PresignPhoto_ShouldThrow_WhenGroupIdIsEmpty()
    {
        _current.Setup(x => x.UserId).Returns(_currentUserId);

        var handler = CreatePhotoHandler();

        var act = () => handler.Handle(
            new PresignPhotoUploadCommand(Guid.Empty, "image/jpeg"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("GroupId is required.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("application/pdf")]
    public async Task PresignPhoto_ShouldThrow_WhenContentTypeUnsupported(string contentType)
    {
        _current.Setup(x => x.UserId).Returns(_currentUserId);

        var handler = CreatePhotoHandler();

        var act = () => handler.Handle(
            new PresignPhotoUploadCommand(_groupId, contentType),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Unsupported content type.");
    }

    [Fact]
    public async Task PresignPhoto_ShouldThrow_WhenUserIsNotActiveMember()
    {
        _current.Setup(x => x.UserId).Returns(_currentUserId);

        _groups.Setup(x => x.IsMemberAsync(_groupId, _currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreatePhotoHandler();

        var act = () => handler.Handle(
            new PresignPhotoUploadCommand(_groupId, "image/jpeg"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not an active member of this group.");
    }

    [Fact]
    public async Task PresignPhoto_ShouldReturnPresign_ForActiveMember()
    {
        _current.Setup(x => x.UserId).Returns(_currentUserId);

        _groups.Setup(x => x.IsMemberAsync(_groupId, _currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _storage.Setup(x => x.GetPresignedUploadUrlAsync(
                It.IsAny<PresignedUploadRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PresignedUploadRequest req, CancellationToken _) => CreatePresignResult(req.Key, req.ContentType));

        var handler = CreatePhotoHandler();

        var result = await handler.Handle(
            new PresignPhotoUploadCommand(_groupId, "image/heic"),
            CancellationToken.None);

        result.UploadUrl.Should().NotBeNullOrWhiteSpace();
        result.StorageKey.Should().Contain("groups/");
        result.StorageKey.Should().Contain("/photos/");
        result.StorageKey.Should().Contain($"{_currentUserId}");
        result.StorageKey.Should().EndWith(".heic");
    }

    [Fact]
    public async Task PresignProfilePhoto_ShouldThrow_WhenUnauthorized()
    {
        _current.Setup(x => x.UserId).Returns(Guid.Empty);

        var handler = CreateProfilePhotoHandler();

        var act = () => handler.Handle(
            new PresignProfilePhotoUploadCommand("image/jpeg"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Unauthorized.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("application/pdf")]
    public async Task PresignProfilePhoto_ShouldThrow_WhenContentTypeUnsupported(string contentType)
    {
        _current.Setup(x => x.UserId).Returns(_currentUserId);

        var handler = CreateProfilePhotoHandler();

        var act = () => handler.Handle(
            new PresignProfilePhotoUploadCommand(contentType),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Unsupported content type.");
    }

    [Fact]
    public async Task PresignProfilePhoto_ShouldReturnPresign_WhenAuthorized()
    {
        _current.Setup(x => x.UserId).Returns(_currentUserId);

        _storage.Setup(x => x.GetPresignedUploadUrlAsync(
                It.IsAny<PresignedUploadRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PresignedUploadRequest req, CancellationToken _) => CreatePresignResult(req.Key, req.ContentType));

        var handler = CreateProfilePhotoHandler();

        var result = await handler.Handle(
            new PresignProfilePhotoUploadCommand("image/webp"),
            CancellationToken.None);

        result.UploadUrl.Should().NotBeNullOrWhiteSpace();
        result.StorageKey.Should().Contain("users/");
        result.StorageKey.Should().Contain("/profile-photo/");
        result.StorageKey.Should().Contain($"{_currentUserId}");
        result.StorageKey.Should().EndWith(".webp");
    }
}