using System.Security.Cryptography;
using System.Text;

namespace DBTracker.Core.Services.Install;

public static class SetupKey
{
    // No 0/O or 1/I/L, so a key read off a screen or a log can be typed back without guessing.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int Length = 24;

    public static string Generate()
    {
        var chars = new char[Length];
        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return FormatForDisplay(new string(chars));
    }

    /// <summary>Groups of four separated by dashes, which is how the key is written to the file and log.</summary>
    public static string FormatForDisplay(string key)
    {
        var normalized = Normalize(key);
        var sb = new StringBuilder();
        for (var i = 0; i < normalized.Length; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                sb.Append('-');
            }
            sb.Append(normalized[i]);
        }
        return sb.ToString();
    }

    public static string Normalize(string? key) =>
        new((key ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public static bool Matches(string expected, string? candidate)
    {
        var a = Encoding.UTF8.GetBytes(Normalize(expected));
        var b = Encoding.UTF8.GetBytes(Normalize(candidate));
        return a.Length > 0 && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
