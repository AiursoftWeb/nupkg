using Aiursoft.Nupkg.Core.Configuration;
using Aiursoft.Nupkg.Core.Upstream;
using Aiursoft.Nupkg.Core.Upstream.Clients;
using Aiursoft.Nupkg.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace Aiursoft.Nupkg.Services.SupplyChain;

/// <summary>Advertises approved versions without downloading their archive files.</summary>
public class S360UpstreamClient(ApprovedPackageMirror mirror, V3UpstreamClient upstream,
    NupkgDbContext db, IOptions<MirrorOptions> options) : IUpstreamClient
{
    private async Task<List<ArtifactIdentity>> ArtifactsAsync(string id, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return [];
        var normalized = id.ToLowerInvariant();
        if (await db.PackageFamilies.AnyAsync(f => f.NormalizedId == normalized && f.OwnerUserId != null, cancellationToken))
            return [];
        return await mirror.ApprovedAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<NuGetVersion>> ListPackageVersionsAsync(string id, CancellationToken cancellationToken) =>
        (await ArtifactsAsync(id, cancellationToken)).Select(a => NuGetVersion.Parse(a.Version)).ToList();

    public async Task<IReadOnlyList<Package>> ListPackagesAsync(string id, CancellationToken cancellationToken)
    {
        var approved = await ArtifactsAsync(id, cancellationToken);
        if (approved.Count == 0) return [];
        var versions = approved.Select(a => NuGetVersion.Parse(a.Version)).ToHashSet();
        var metadata = await upstream.ListPackagesAsync(id, cancellationToken);
        var result = metadata.Where(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && versions.Contains(p.Version)).ToList();
        if (result.Count == 0) throw new HttpRequestException("Approved package metadata is unavailable at its upstream.");
        return result;
    }

    // Exact content requests go through ApprovedPackageMirror in the admission
    // middleware. Never let the legacy core fallback index an unverified stream.
    public Task<Stream> DownloadPackageOrNullAsync(string id, NuGetVersion version, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(null!);
}
