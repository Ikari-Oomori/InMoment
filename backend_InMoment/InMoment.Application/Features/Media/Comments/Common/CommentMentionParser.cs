using System.Text.RegularExpressions;

namespace InMoment.Application.Features.Media.Comments.Common;

internal static class CommentMentionParser
{
    private static readonly Regex MentionRegex = new(
        @"(?<![\w@])@([A-Za-zА-Яа-яЁё0-9_\.]{2,50})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> ExtractUserNames(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        var result = MentionRegex.Matches(text)
            .Select(x => x.Groups[1].Value.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        return result;
    }
}