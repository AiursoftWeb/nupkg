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

    private string PolicySource => admission.PolicySource(Source);

    private static bool ValidName(string name) => name.Length is > 0 and <= 128 &&
        name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');

    private void Validate(ArtifactIdentity artifact)
    {
        // Never let discovery select another registry, name pattern or archive identity.
        if (artifact.Ecosystem != "nuget" || artifact.Source.TrimEnd('/') != PolicySource ||
            !ValidName(artifact.Name) || !NuGetVersion.TryParse(artifact.Version, out _) ||
            artifact.Sha256.Length != 64 || !artifact.Sha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("S360 returned an invalid mirror artifact.");
    }

    public async Task<List<PackageMirrorCandidate>> CandidatesAsync(string name, CancellationToken cancellationToken)
    {
        if (!ValidName(name)) return [];
        var result = new List<PackageMirrorCandidate>();
        await foreach (var artifact in admission.ApprovedAsync(PolicySource, cancellationToken, name))
        {
            Validate(artifact);
            if (!artifact.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("S360 returned an unrelated package.");
            result.Add(new PackageMirrorCandidate(artifact.Name, artifact.Version, artifact.Sha256));
        }
        if (admission.UsesRegistryPolicy)
        {
            var versions = await upstream.ListPackageVersionsAsync(name, cancellationToken);
            var candidates = await admission.CandidatesAsync(Source, name,
                versions.Select(v => v.ToNormalizedString()).Concat(result.Select(a => a.Version)), cancellationToken);
            var allowed = candidates.Select(a => NuGetVersion.Parse(a.Version)).ToHashSet();
            result = result.Where(a => allowed.Contains(NuGetVersion.Parse(a.Version))).ToList();
            result.AddRange(candidates.Where(c => result.All(a => NuGetVersion.Parse(a.Version) != NuGetVersion.Parse(c.Version)))
                .Select(c => new PackageMirrorCandidate(c.Name, c.Version, null)));
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
        await foreach (var artifact in admission.ApprovedAsync(PolicySource, cancellationToken))
        {
            Validate(artifact);
            if (await EnsureAsync(artifact.Name, NuGetVersion.Parse(artifact.Version),
                new PackageMirrorCandidate(artifact.Name, artifact.Version, artifact.Sha256), cancellationToken)
                == PackageMirrorResult.Mirrored) count++;
        }
        return count;
    }

    public Task<PackageMirrorResult> EnsureAsync(string name, NuGetVersion version, CancellationToken cancellationToken) =>
        EnsureAsync(name, version, null, cancellationToken);

    private async Task<PackageMirrorResult> EnsureAsync(string name, NuGetVersion version,
        PackageMirrorCandidate? discovered, CancellationToken cancellationToken)
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
            if (await db.PackageFamilies.AnyAsync(f => f.NormalizedId == id && (f.IsLocallyPublished || f.OwnerUserId != null), cancellationToken))
                return PackageMirrorResult.Denied;
            if (existing is { MirrorSource: not null, MirrorVersion: not null, MirrorSha256: not null })
                return PackageMirrorResult.Available; // The read middleware still reassesses this provenance.
            var candidate = discovered ?? (await CandidatesAsync(name, cancellationToken))
                .SingleOrDefault(a => NuGetVersion.Parse(a.Version) == version);
            if (candidate == null) return PackageMirrorResult.Denied;
            try
            {
                await using var stream = existing == null
                    ? await upstream.DownloadPackageOrNullAsync(candidate.Name, version, cancellationToken)
                    : await storage.GetPackageStreamAsync(existing.Id, existing.Version, cancellationToken);
                if (stream == null) return PackageMirrorResult.Unavailable;
                stream.Position = 0;
                var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
                if (candidate.Sha256 != null && !hash.Equals(candidate.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Archive SHA256 does not match S360.");
                stream.Position = 0;
                using (var reader = new PackageArchiveReader(stream, leaveStreamOpen: true))
                {
                    var identity = await reader.GetIdentityAsync(cancellationToken);
                    if (!identity.Id.Equals(candidate.Name, StringComparison.OrdinalIgnoreCase) || identity.Version != version)
                        throw new InvalidDataException("Archive identity does not match S360.");
                }
                var artifact = new ArtifactIdentity("nuget", candidate.Name, candidate.Version, PolicySource, hash);
                var decision = admission.UsesRegistryPolicy
                    ? await admission.VerifyMirrorAsync(artifact, cancellationToken)
                    : (await admission.AssessAsync([artifact], cancellationToken)).Single();
                if (!decision.Trusted || decision.PendingVerification) return PackageMirrorResult.Denied;
                if (existing != null)
                {
                    existing.MirrorSource = Source;
                    existing.MirrorVersion = candidate.Version;
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
                        MirrorSource = Source, MirrorVersion = candidate.Version, MirrorSha256 = artifact.Sha256
                    }, CancellationToken.None);
                    if (result != PackageIndexingResult.Success) return PackageMirrorResult.Unavailable;
                }
                logger.LogInformation("Mirrored approved artifact {PackageId} {Version}.", candidate.Name, candidate.Version);
                return PackageMirrorResult.Mirrored;
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                logger.LogWarning("Could not mirror approved artifact {PackageId} {Version}: {Reason}",
                    candidate.Name, candidate.Version, ex.Message);
                return PackageMirrorResult.Unavailable;
            }
        }
        finally { gate.Release(); }
    }
}
