using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SmartSupport.Api.Data;
using SmartSupport.Api.Models;

namespace SmartSupport.Api.Services;

/// <summary>
/// Works out who the caller is and which organization (and role) they have.
/// Every org-scoped endpoint goes through this, so the check can't be forgotten.
/// </summary>
public class OrgAccessService
{
    // No 0/O/1/I/L, so codes are easy to read out loud and type.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private readonly AppDbContext _db;

    public OrgAccessService(AppDbContext db)
    {
        _db = db;
    }

    public static string? GetGoogleId(ClaimsPrincipal principal) =>
        principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    /// <summary>Finds the signed-in user's record, creating it on first sign-in.</summary>
    public async Task<User> GetOrCreateUserAsync(ClaimsPrincipal principal)
    {
        var googleId = GetGoogleId(principal)
            ?? throw new UnauthorizedAccessException("Token has no subject.");
        var email = principal.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        var name = principal.FindFirst("name")?.Value;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId);
        if (user == null)
        {
            user = new User { GoogleId = googleId, Email = email, Name = name };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }
        else if (name != null && user.Name != name)
        {
            user.Name = name;
            await _db.SaveChangesAsync();
        }

        return user;
    }

    /// <summary>The caller's membership (with user and organization loaded), or null if they have not joined one yet.</summary>
    public async Task<Membership?> GetMembershipAsync(ClaimsPrincipal principal)
    {
        var googleId = GetGoogleId(principal);
        if (googleId == null) return null;

        return await _db.Memberships
            .Include(m => m.User)
            .Include(m => m.Organization)
            .FirstOrDefaultAsync(m => m.User.GoogleId == googleId);
    }

    /// <summary>Creates a random 8-character code that no other organization uses.</summary>
    public async Task<string> GenerateUniqueInviteCodeAsync()
    {
        while (true)
        {
            var code = RandomNumberGenerator.GetString(CodeAlphabet, 8);
            if (!await _db.Organizations.AnyAsync(o => o.InviteCode == code))
                return code;
        }
    }

    /// <summary>Accepts "k7p2-xq9m", "K7P2XQ9M", a pasted link, etc. and returns "K7P2XQ9M".</summary>
    public static string NormalizeInviteCode(string input)
    {
        var lastPart = input.Trim().TrimEnd('/').Split('/').Last();
        return new string(lastPart.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

    /// <summary>"K7P2XQ9M" becomes "K7P2-XQ9M" for display.</summary>
    public static string FormatInviteCode(string code) =>
        code.Length == 8 ? $"{code[..4]}-{code[4..]}" : code;
}
