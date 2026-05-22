using FluentAssertions;
using InMoment.Infrastructure.Auth;

namespace InMoment.Infrastructure.Tests.Auth;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Hash_ShouldProduceDifferentHashes_ForSamePassword()
    {
        var hasher = new PasswordHasher();

        var first = hasher.Hash("Pa$$word123");
        var second = hasher.Hash("Pa$$word123");

        first.Should().NotBeNullOrWhiteSpace();
        second.Should().NotBeNullOrWhiteSpace();
        first.Should().NotBe(second);
    }

    [Fact]
    public void Verify_ShouldReturnTrue_ForCorrectPassword()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("Pa$$word123");

        var result = hasher.Verify("Pa$$word123", hash);

        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_ShouldReturnFalse_ForWrongPassword()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("Pa$$word123");

        var result = hasher.Verify("wrong-password", hash);

        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_ShouldReturnFalse_ForMalformedHash()
    {
        var hasher = new PasswordHasher();

        hasher.Verify("test", "").Should().BeFalse();
        hasher.Verify("test", "not.valid.hash").Should().BeFalse();
        hasher.Verify("test", "abc.def.ghi").Should().BeFalse();
    }
}