using System.Security.Cryptography;

namespace SmartSupport.Application.Organizations;

/// <summary>Rules for organization invite codes like "K7P2-XQ9M".</summary>
public static class InviteCode
{
    public const int Length = 8;

    // No 0/O/1/I/L, so codes are easy to read out loud and type. 31^8 ≈ 850 billion combinations.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>A new random code (cryptographically secure), stored without the dash.</summary>
    public static string Generate() => RandomNumberGenerator.GetString(Alphabet, Length);

    /// <summary>Accepts "k7p2-xq9m", "K7P2XQ9M" or a pasted invite link and returns "K7P2XQ9M".</summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var lastPart = input.Trim().TrimEnd('/').Split('/').Last();
        return new string(lastPart.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

    /// <summary>"K7P2XQ9M" becomes "K7P2-XQ9M" for display.</summary>
    public static string Format(string code) =>
        code.Length == Length ? $"{code[..4]}-{code[4..]}" : code;
}
