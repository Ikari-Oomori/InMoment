using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using InMoment.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Moq;

namespace InMoment.Infrastructure.Tests.Storage;

public sealed class S3FileStorageTests
{
    [Fact]
    public async Task GetPresignedUploadUrlAsync_ShouldBuildResult_WithCustomExpiry()
    {
        var s3 = new Mock<IAmazonS3>();
        var options = CreateOptions(
            endpoint: "https://storage.example.com",
            bucket: "inmoment",
            publicBaseUrl: "https://cdn.example.com");

        GetPreSignedUrlRequest? capturedRequest = null;

        s3.Setup(x => x.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
            .Callback<GetPreSignedUrlRequest>(r => capturedRequest = r)
            .Returns("https://signed.example.com/upload-url");

        var storage = new S3FileStorage(s3.Object, options);

        var before = DateTimeOffset.UtcNow;

        var result = await storage.GetPresignedUploadUrlAsync(
            new PresignedUploadRequest(
                "groups/family/photo.jpg",
                "image/jpeg",
                TimeSpan.FromMinutes(5)),
            CancellationToken.None);

        var after = DateTimeOffset.UtcNow;

        capturedRequest.Should().NotBeNull();
        capturedRequest!.BucketName.Should().Be("inmoment");
        capturedRequest.Key.Should().Be("groups/family/photo.jpg");
        capturedRequest.ContentType.Should().Be("image/jpeg");
        capturedRequest.Verb.Should().Be(HttpVerb.PUT);

        result.UploadUrl.Should().Be("https://signed.example.com/upload-url");
        result.Key.Should().Be("groups/family/photo.jpg");
        result.FileUrl.Should().Be("https://cdn.example.com/groups/family/photo.jpg");
        result.ExpiresAt.Should().BeAfter(before.AddMinutes(4));
        result.ExpiresAt.Should().BeBefore(after.AddMinutes(6));
    }

    [Fact]
    public async Task GetPresignedUploadUrlAsync_ShouldUseDefaultExpiryFromOptions_WhenRequestExpiryMissing()
    {
        var s3 = new Mock<IAmazonS3>();
        var options = CreateOptions(
            endpoint: "https://storage.example.com",
            bucket: "inmoment",
            publicBaseUrl: "https://cdn.example.com",
            presignExpiryMinutes: 12);

        GetPreSignedUrlRequest? capturedRequest = null;

        s3.Setup(x => x.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
            .Callback<GetPreSignedUrlRequest>(r => capturedRequest = r)
            .Returns("https://signed.example.com/upload-url");

        var storage = new S3FileStorage(s3.Object, options);

        var before = DateTimeOffset.UtcNow;

        var result = await storage.GetPresignedUploadUrlAsync(
            new PresignedUploadRequest("avatars/user.jpg", "image/jpeg"),
            CancellationToken.None);

        var after = DateTimeOffset.UtcNow;

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Expires.Should().BeAfter(before.AddMinutes(11).UtcDateTime);
        capturedRequest.Expires.Should().BeBefore(after.AddMinutes(13).UtcDateTime);

        result.ExpiresAt.Should().BeAfter(before.AddMinutes(11));
        result.ExpiresAt.Should().BeBefore(after.AddMinutes(13));
    }

    [Fact]
    public async Task GetPresignedUploadUrlAsync_ShouldTrimKey_AndContentType()
    {
        var s3 = new Mock<IAmazonS3>();
        var options = CreateOptions(
            endpoint: "https://storage.example.com",
            bucket: "inmoment",
            publicBaseUrl: "https://cdn.example.com");

        GetPreSignedUrlRequest? capturedRequest = null;

        s3.Setup(x => x.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
            .Callback<GetPreSignedUrlRequest>(r => capturedRequest = r)
            .Returns("https://signed.example.com/upload-url");

        var storage = new S3FileStorage(s3.Object, options);

        var result = await storage.GetPresignedUploadUrlAsync(
            new PresignedUploadRequest("  /groups/family/photo.jpg  ", "  image/jpeg  "),
            CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Key.Should().Be("groups/family/photo.jpg");
        capturedRequest.ContentType.Should().Be("image/jpeg");

        result.Key.Should().Be("groups/family/photo.jpg");
        result.FileUrl.Should().Be("https://cdn.example.com/groups/family/photo.jpg");
    }

    [Fact]
    public async Task GetPresignedUploadUrlAsync_ShouldThrow_WhenKeyMissing()
    {
        var s3 = new Mock<IAmazonS3>();
        var options = CreateOptions(
            endpoint: "https://storage.example.com",
            bucket: "inmoment",
            publicBaseUrl: "https://cdn.example.com");

        var storage = new S3FileStorage(s3.Object, options);

        var act = () => storage.GetPresignedUploadUrlAsync(
            new PresignedUploadRequest("   ", "image/jpeg"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Key is required.");

        s3.Verify(x => x.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()), Times.Never);
    }

    [Fact]
    public async Task GetPresignedUploadUrlAsync_ShouldThrow_WhenContentTypeMissing()
    {
        var s3 = new Mock<IAmazonS3>();
        var options = CreateOptions(
            endpoint: "https://storage.example.com",
            bucket: "inmoment",
            publicBaseUrl: "https://cdn.example.com");

        var storage = new S3FileStorage(s3.Object, options);

        var act = () => storage.GetPresignedUploadUrlAsync(
            new PresignedUploadRequest("groups/family/photo.jpg", "   "),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("ContentType is required.");

        s3.Verify(x => x.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()), Times.Never);
    }

    [Fact]
    public async Task GetPresignedUploadUrlAsync_ShouldNormalizeLocalhostScheme_WhenEndpointIsHttp()
    {
        var s3 = new Mock<IAmazonS3>();
        var options = CreateOptions(
            endpoint: "http://localhost:9000",
            bucket: "inmoment",
            publicBaseUrl: "http://localhost:9000/inmoment");

        s3.Setup(x => x.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
            .Returns("https://localhost:9000/inmoment/groups/family/photo.jpg?sig=1");

        var storage = new S3FileStorage(s3.Object, options);

        var result = await storage.GetPresignedUploadUrlAsync(
            new PresignedUploadRequest("groups/family/photo.jpg", "image/jpeg"),
            CancellationToken.None);

        result.UploadUrl.Should().StartWith("http://localhost:9000/");
    }

    [Fact]
    public void GetPublicUrl_ShouldTrimSlashes()
    {
        var s3 = new Mock<IAmazonS3>();
        var options = CreateOptions(
            endpoint: "https://storage.example.com",
            bucket: "inmoment",
            publicBaseUrl: "https://cdn.example.com/");

        var storage = new S3FileStorage(s3.Object, options);

        var result = storage.GetPublicUrl("/groups/family/photo.jpg");

        result.Should().Be("https://cdn.example.com/groups/family/photo.jpg");
    }

    [Fact]
    public void GetPublicUrl_ShouldThrow_WhenKeyMissing()
    {
        var s3 = new Mock<IAmazonS3>();
        var options = CreateOptions(
            endpoint: "https://storage.example.com",
            bucket: "inmoment",
            publicBaseUrl: "https://cdn.example.com/");

        var storage = new S3FileStorage(s3.Object, options);

        var act = () => storage.GetPublicUrl("   ");

        act.Should().Throw<ValidationException>()
            .WithMessage("Key is required.");
    }

    [Fact]
    public void GetPublicUrl_ShouldThrow_WhenPublicBaseUrlMissing()
    {
        var s3 = new Mock<IAmazonS3>();
        var options = CreateOptions(
            endpoint: "https://storage.example.com",
            bucket: "inmoment",
            publicBaseUrl: "   ");

        var storage = new S3FileStorage(s3.Object, options);

        var act = () => storage.GetPublicUrl("groups/family/photo.jpg");

        act.Should().Throw<ValidationException>()
            .WithMessage("Storage public base url is required.");
    }

    [Fact]
    public async Task DeleteAsync_ShouldCallS3DeleteObject()
    {
        var s3 = new Mock<IAmazonS3>();
        var options = CreateOptions(
            endpoint: "https://storage.example.com",
            bucket: "inmoment",
            publicBaseUrl: "https://cdn.example.com");

        DeleteObjectRequest? capturedRequest = null;

        s3.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteObjectRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new DeleteObjectResponse());

        var storage = new S3FileStorage(s3.Object, options);

        await storage.DeleteAsync("groups/family/photo.jpg", CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.BucketName.Should().Be("inmoment");
        capturedRequest.Key.Should().Be("groups/family/photo.jpg");
    }

    [Fact]
    public async Task DeleteAsync_ShouldTrimKey()
    {
        var s3 = new Mock<IAmazonS3>();
        var options = CreateOptions(
            endpoint: "https://storage.example.com",
            bucket: "inmoment",
            publicBaseUrl: "https://cdn.example.com");

        DeleteObjectRequest? capturedRequest = null;

        s3.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteObjectRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new DeleteObjectResponse());

        var storage = new S3FileStorage(s3.Object, options);

        await storage.DeleteAsync("  /groups/family/photo.jpg  ", CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Key.Should().Be("groups/family/photo.jpg");
    }

    [Fact]
    public async Task DeleteAsync_ShouldIgnoreBlankKey()
    {
        var s3 = new Mock<IAmazonS3>();
        var options = CreateOptions(
            endpoint: "https://storage.example.com",
            bucket: "inmoment",
            publicBaseUrl: "https://cdn.example.com");

        var storage = new S3FileStorage(s3.Object, options);

        await storage.DeleteAsync("   ", CancellationToken.None);

        s3.Verify(
            x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static IOptions<StorageOptions> CreateOptions(
        string endpoint,
        string bucket,
        string publicBaseUrl,
        int presignExpiryMinutes = 10)
    {
        return Options.Create(new StorageOptions
        {
            Endpoint = endpoint,
            AccessKey = "access",
            SecretKey = "secret",
            Bucket = bucket,
            PublicBaseUrl = publicBaseUrl,
            Region = "auto",
            PresignExpiryMinutes = presignExpiryMinutes
        });
    }
}