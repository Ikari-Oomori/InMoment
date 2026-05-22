namespace InMoment.Domain.Common;

public static class PhoneNumberNormalizer
{
    public static string? Normalize(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        var trimmed = phoneNumber.Trim();
        if (trimmed.Length == 0)
            return null;

        var buffer = new List<char>(trimmed.Length);

        foreach (var ch in trimmed)
        {
            if (char.IsDigit(ch))
            {
                buffer.Add(ch);
                continue;
            }

            if (ch == '+' && buffer.Count == 0)
                buffer.Add(ch);
        }

        if (buffer.Count == 0)
            return null;

        if (buffer.Count == 1 && buffer[0] == '+')
            return null;

        return new string(buffer.ToArray());
    }
}