using InMoment.Application.Features.Notifications.List;

namespace InMoment.Tests.Application.Tests.Notifications.List;

public sealed class NotificationTimeTextBuilderTests
{
    private static readonly DateTime Now = new(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BuildRu_ShouldReturnOnlyNow_WhenDifferenceLessThan15Seconds()
    {
        var createdAt = Now.AddSeconds(-10);

        var result = NotificationTimeTextBuilder.BuildRu(createdAt, Now);

        result.Should().Be("только что");
    }

    [Fact]
    public void BuildRu_ShouldClampFutureDatesToOnlyNow()
    {
        var createdAt = Now.AddMinutes(5);

        var result = NotificationTimeTextBuilder.BuildRu(createdAt, Now);

        result.Should().Be("только что");
    }

    [Fact]
    public void BuildRu_ShouldReturnLessThanMinute_WhenDifferenceLessThanMinute()
    {
        var createdAt = Now.AddSeconds(-40);

        var result = NotificationTimeTextBuilder.BuildRu(createdAt, Now);

        result.Should().Be("меньше минуты назад");
    }

    [Theory]
    [InlineData(1, "1 минуту назад")]
    [InlineData(2, "2 минуты назад")]
    [InlineData(5, "5 минут назад")]
    [InlineData(21, "21 минуту назад")]
    [InlineData(24, "24 минуты назад")]
    [InlineData(25, "25 минут назад")]
    public void BuildRu_ShouldPluralizeMinutes(int minutes, string expected)
    {
        var createdAt = Now.AddMinutes(-minutes);

        var result = NotificationTimeTextBuilder.BuildRu(createdAt, Now);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1, "1 час назад")]
    [InlineData(2, "2 часа назад")]
    [InlineData(5, "5 часов назад")]
    [InlineData(21, "21 час назад")]
    [InlineData(22, "22 часа назад")]
    [InlineData(25, "25 часов назад")]
    public void BuildRu_ShouldPluralizeHours_WhenLessThan24Hours(int hours, string expected)
    {
        var createdAt = Now.AddHours(-hours);

        var result = NotificationTimeTextBuilder.BuildRu(createdAt, Now);

        if (hours < 24)
        {
            result.Should().Be(expected);
        }
        else
        {
            result.Should().NotBe(expected);
        }
    }

    [Fact]
    public void BuildRu_ShouldReturnYesterday_WhenDifferenceIsOneDay()
    {
        var createdAt = Now.AddDays(-1);

        var result = NotificationTimeTextBuilder.BuildRu(createdAt, Now);

        result.Should().Be("вчера");
    }

    [Theory]
    [InlineData(2, "2 дня назад")]
    [InlineData(5, "5 дней назад")]
    [InlineData(6, "6 дней назад")]
    public void BuildRu_ShouldPluralizeDays_WhenLessThanSevenDays(int days, string expected)
    {
        var createdAt = Now.AddDays(-days);

        var result = NotificationTimeTextBuilder.BuildRu(createdAt, Now);

        result.Should().Be(expected);
    }

    [Fact]
    public void BuildRu_ShouldReturnFormattedLocalDate_WhenDifferenceAtLeastSevenDays()
    {
        var createdAt = new DateTime(2026, 3, 20, 8, 30, 0, DateTimeKind.Utc);

        var result = NotificationTimeTextBuilder.BuildRu(createdAt, Now);

        result.Should().Be(createdAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
    }
}