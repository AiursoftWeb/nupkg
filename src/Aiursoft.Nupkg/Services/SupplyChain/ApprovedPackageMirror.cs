using System.Security.Cryptography;
using Aiursoft.Nupkg.Core.Authentication;
using Aiursoft.Nupkg.Core.Configuration;
using Aiursoft.Nupkg.Core.Indexing;
using Aiursoft.Nupkg.Core.Storage;
using Aiursoft.Nupkg.Core.Upstream.Clients;
using Aiursoft.Nupkg.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NuGet.Packaging;
using NuGet.Versioning;

namespace Aiursoft.Nupkg.Services.SupplyChain;

public enum PackageMirrorResult { Available, Mirrored, Denied, Unavailable }

public class ApprovedPackageMirror(S360AdmissionClient admission, V3UpstreamClient upstream,
    IPackageIndexingService indexer, IPackageStorageService storage, NupkgDbContext db,
    IOptions<MirrorOptions> mirrorOptions, IOptions<S360MirrorOptions> options, PackageMirrorLocks locks,
    ILogger<ApprovedPackageMirror> logger)
{
    private string Source
    {
        get
        {
            if (mirrorOptions.Value.Legacy || mirrorOptions.Value.PackageSource == null)
                throw new InvalidOperationException("S360 controlled mirroring requires a NuGet v3 source.");
            return mirrorOptions.Value.PackageSource.AbsoluteUri.TrimEnd('/');
        }
    }

    private static bool ValidName(string name) => name.Length is > 0 and <= 128 &&
        name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');

    private void Validate(ArtifactIdentity artifact)
    {
        // Never let discovery select another registry, name pattern or archive identity.
        if (artifact.Ecosystem != "nuget" || artifact.Source.TrimEnd('/') != Source ||
            !ValidName(artifact.Name) || !NuGetVersion.TryParse(artifact.Version, out _) ||
            artifact.Sha256.Length != 64 || !artifact.Sha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("S360 returned an invalid mirror artifact.");
    }

    public async Task<List<ArtifactIdentity>> ApprovedAsync(string name, CancellationToken cancellationToken)
    {
        if (!ValidName(name)) return [];
        var result = new List<ArtifactIdentity>();
        await foreach (var artifact in admission.ApprovedAsync(Source, cancellationToken, name))
        {
            Validate(artifact);
            if (!artifact.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("S360 returned an unrelated package.");
            result.Add(artifact);
        }
        // NuGet normalizes equivalent version strings. Conflicting bytes must not be
        // published as one normalized version, even if origin records differ in spelling.
        return result.GroupBy(a => NuGetVersion.Parse(a.Version)).Where(g =>
            g.Select(a => a.Sha256).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
            .Select(g => g.First()).ToList();
    }

    public async Task<int> SynchronizeAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || !mirrorOptions.Value.Enabled) return 0;
        var count = 0;
        await foreach (var artifact in admission.ApprovedAsync(Source, cancellationToken))
        {
            Validate(artifact);
            if (await EnsureAsync(artifact.Name, NuGetVersion.Parse(artifact.Version), artifact, cancellationToken)
                == PackageMirrorResult.Mirrored) count++;
        }
        return count;
    }

    public Task<PackageMirrorResult> EnsureAsync(string name, NuGetVersion version, CancellationToken cancellationToken) =>
        EnsureAsync(name, version, null, cancellationToken);

    private async Task<PackageMirrorResult> EnsureAsync(string name, NuGetVersion version,
        ArtifactIdentity? discovered, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || !mirrorOptions.Value.Enabled || !ValidName(name))
            return PackageMirrorResult.Denied;
        var normalized = version.ToNormalizedString().ToLowerInvariant();
        var id = name.ToLowerInvariant();
        var gate = locks.For(id, normalized);
        await gate.WaitAsync(cancellationToken);
        try
        {
            // Recheck after waiting: another request may have completed the same download.
            var existing = await db.Packages.IgnoreQueryFilters().SingleOrDefaultAsync(p =>
                p.Id.ToLower() == id && p.NormalizedVersionString == normalized, cancellationToken);
            if (existing is { IsLocallyPublished: true } || existing?.UploadedByUserId != null)
                return PackageMirrorResult.Available;
            if (existing is { MirrorSource: not null, MirrorVersion: not null, MirrorSha256: not null })
                return PackageMirrorResult.Available; // The read middleware still reassesses this provenance.
            if (await db.PackageFamilies.AnyAsync(f => f.NormalizedId == id && f.OwnerUserId != null, cancellationToken))
                return PackageMirrorResult.Denied;
            var artifact = discovered ?? (await ApprovedAsync(name, cancellationToken))
                .SingleOrDefault(a => NuGetVersion.Parse(a.Version) == version);
            if (artifact == null) return PackageMirrorResult.Denied;
            Validate(artifact);
            try
            {
                await using var stream = existing == null
                    ? await upstream.DownloadPackageOrNullAsync(artifact.Name, version, cancellationToken)
                    : await storage.GetPackageStreamAsync(existing.Id, existing.Version, cancellationToken);
                if (stream == null) return PackageMirrorResult.Unavailable;
                stream.Position = 0;
                var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
                if (!hash.Equals(artifact.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Archive SHA256 does not match S360.");
                stream.Position = 0;
                using (var reader = new PackageArchiveReader(stream, leaveStreamOpen: true))
                {
                    var identity = await reader.GetIdentityAsync(cancellationToken);
                    if (!identity.Id.Equals(artifact.Name, StringComparison.OrdinalIgnoreCase) || identity.Version != version)
                        throw new InvalidDataException("Archive identity does not match S360.");
                }
                if (!(await admission.AssessAsync([artifact], cancellationToken)).Single().Trusted)
                    return PackageMirrorResult.Denied;
                if (existing != null)
                {
                    existing.MirrorSource = artifact.Source;
                    existing.MirrorVersion = artifact.Version;
                    existing.MirrorSha256 = artifact.Sha256;
                    await db.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    stream.Position = 0;
                    cancellationToken.ThrowIfCancellationRequested();
                    // Once verified bytes begin committing, finish the existing storage/index
                    // operation even if this client disconnects. Other callers wait on the gate.
                    var result = await indexer.IndexAsync(stream, PackagePublisher.System with
                    {
                        MirrorSource = artifact.Source, MirrorVersion = artifact.Version, MirrorSha256 = artifact.Sha256
                    }, CancellationToken.None);
                    if (result != PackageIndexingResult.Success) return PackageMirrorResult.Unavailable;
                }
                logger.LogInformation("Mirrored approved artifact {PackageId} {Version}.", artifact.Name, artifact.Version);
                return PackageMirrorResult.Mirrored;
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                logger.LogWarning("Could not mirror approved artifact {PackageId} {Version}: {Reason}",
                    artifact.Name, artifact.Version, ex.Message);
                return PackageMirrorResult.Unavailable;
            }
        }
        finally { gate.Release(); }
    }
}
