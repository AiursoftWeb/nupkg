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

public class ApprovedPackageMirror(S360AdmissionClient admission, V3UpstreamClient upstream,
    IPackageIndexingService indexer, IPackageStorageService storage, NupkgDbContext db,
    IOptions<MirrorOptions> mirrorOptions, IOptions<S360MirrorOptions> options,
    ILogger<ApprovedPackageMirror> logger)
{
    public async Task<int> SynchronizeAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || !mirrorOptions.Value.Enabled) return 0;
        if (mirrorOptions.Value.Legacy || mirrorOptions.Value.PackageSource == null)
            throw new InvalidOperationException("S360 controlled mirroring requires a NuGet v3 source.");
        var source = mirrorOptions.Value.PackageSource.AbsoluteUri.TrimEnd('/');
        var count = 0;
        await foreach (var artifact in admission.ApprovedAsync(source, cancellationToken))
        {
            // S360 cannot instruct this job to fetch an arbitrary registry or a wildcard.
            if (artifact.Ecosystem != "nuget" || artifact.Source.TrimEnd('/') != source ||
                !NuGetVersion.TryParse(artifact.Version, out var version) ||
                artifact.Name.Length is < 1 or > 128 ||
                !artifact.Name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_') ||
                artifact.Sha256.Length != 64 || !artifact.Sha256.All(Uri.IsHexDigit))
                throw new InvalidDataException("S360 returned an invalid mirror artifact.");
            var normalized = version.ToNormalizedString().ToLowerInvariant();
            var id = artifact.Name.ToLowerInvariant();
            var existing = await db.Packages.IgnoreQueryFilters().SingleOrDefaultAsync(p =>
                p.Id.ToLower() == id && p.NormalizedVersionString == normalized, cancellationToken);
            if (existing is { IsLocallyPublished: true } || existing?.UploadedByUserId != null) continue;
            if (existing?.MirrorSource == artifact.Source && existing.MirrorVersion == artifact.Version &&
                existing.MirrorSha256 == artifact.Sha256) continue;
            try
            {
                // Reuse legacy cached bytes only after checking their actual digest. Never
                // overwrite a conflicting local archive, even with AllowPackageOverwrites.
                await using var stream = existing == null
                    ? await upstream.DownloadPackageOrNullAsync(artifact.Name, version, cancellationToken)
                    : await storage.GetPackageStreamAsync(existing.Id, existing.Version, cancellationToken);
                if (stream == null) throw new InvalidDataException("Approved archive is missing at its origin.");
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
                // Approval may have changed while the archive was downloading.
                if (!(await admission.AssessAsync([artifact], cancellationToken)).Single().Trusted) continue;
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
                    var result = await indexer.IndexAsync(stream, PackagePublisher.System with
                    {
                        MirrorSource = artifact.Source, MirrorVersion = artifact.Version, MirrorSha256 = artifact.Sha256
                    }, cancellationToken);
                    if (result != PackageIndexingResult.Success && result != PackageIndexingResult.PackageAlreadyExists)
                        throw new InvalidDataException($"Approved archive could not be indexed: {result}.");
                }
                count++;
                logger.LogInformation("Mirrored approved artifact {PackageId} {Version}.", artifact.Name, artifact.Version);
            }
            catch (Exception ex) when (ex is InvalidDataException or HttpRequestException or IOException)
            {
                logger.LogWarning("Could not mirror approved artifact {PackageId} {Version}: {Reason}",
                    artifact.Name, artifact.Version, ex.Message);
            }
        }
        return count;
    }
}
