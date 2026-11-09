using System.Security.Cryptography;
using System.Text;
using Aiursoft.Nupkg.Authorization;
using Aiursoft.Nupkg.Core.Authentication;
using Aiursoft.Nupkg.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LegacyAuthentication = Aiursoft.Nupkg.Core.Authentication.IAuthenticationService;

namespace Aiursoft.Nupkg.Services.Authentication;

public class PackagePublisherAuthenticationService(
    NupkgDbContext db,
    IUserClaimsPrincipalFactory<User> claimsFactory,
    LegacyAuthentication legacyAuthentication)
{
    public static string ComputeSha256Hex(string rawKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();

    public async Task<PackagePublisher?> AuthenticateAsync(string rawKey, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(rawKey))
        {
            var hash = ComputeSha256Hex(rawKey);
            var key = await db.UserApiKeys.Include(k => k.User)
                .SingleOrDefaultAsync(k => k.KeyHash == hash, cancellationToken);
            if (key != null)
            {
                if (key.IsExpired || key.User == null) return null;
                key.LastUsedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return await ForUserAsync(key.User);
            }
        }

        return await legacyAuthentication.AuthenticateAsync(rawKey, cancellationToken)
            ? PackagePublisher.System : null;
    }

    public async Task<PackagePublisher> ForUserAsync(User user)
    {
        var principal = await claimsFactory.CreateAsync(user);
        return new PackagePublisher(user.Id,
            principal.HasClaim(AppPermissions.Type, AppPermissionNames.CanPublishPackages),
            principal.HasClaim(AppPermissions.Type, AppPermissionNames.CanManagePackages));
    }
}
