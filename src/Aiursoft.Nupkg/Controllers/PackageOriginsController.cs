using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Aiursoft.Nupkg.Core.Storage;
using Aiursoft.Nupkg.Entities;
using NuGet.Versioning;

namespace Aiursoft.Nupkg.Controllers;

[AllowAnonymous, Route("api/package-origins")]
public class PackageOriginsController(NupkgDbContext db, IPackageStorageService storage) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string name, string version, CancellationToken token)
    {
        Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrEmpty(name) || name.Length > 128 || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-')) ||
            !NuGetVersion.TryParse(version, out var parsed)) return BadRequest();
        var id = name.ToLowerInvariant();
        var normalized = parsed.ToNormalizedString().ToLowerInvariant();
        var local = await db.PackageFamilies.AsNoTracking().AnyAsync(f => f.NormalizedId == id &&
            (f.IsLocallyPublished || f.OwnerUserId != null), token);
        string? hash = null;
        string? archive = null;
        if (local && await db.Packages.IgnoreQueryFilters().AsNoTracking().AnyAsync(p =>
                p.Id.ToLower() == id && p.NormalizedVersionString == normalized &&
                (p.IsLocallyPublished || p.UploadedByUserId != null), token))
        {
            await using var stream = await storage.GetPackageStreamAsync(id, parsed, token);
            hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, token));
            archive = $"{Request.PathBase}/v3/package/{id}/{normalized}/{id}.{normalized}.nupkg";
        }
        // Only publication state comes from this endpoint. Downloads keep their existing routes.
        return Ok(new { ecosystem = "nuget", name = id, version = normalized, mode = local ? "local" : "mirror", sha256 = hash, archive });
    }
}
