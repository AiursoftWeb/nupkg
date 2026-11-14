using Aiursoft.Nupkg.Entities;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;
using Aiursoft.Nupkg.Core.Authentication;

namespace Aiursoft.Nupkg.Core
{
    public class PackageDatabase(NupkgDbContext context)
    {
        private readonly NupkgDbContext _context = context ?? throw new ArgumentNullException(nameof(context));

        public async Task<PackageAddResult> AddAsync(Package package, CancellationToken cancellationToken)
        {
            if (package.PackageFamilyId == 0)
            {
                var registration = await GetOrCreateFamilyAsync(package, PackagePublisher.System, cancellationToken);
                package.PackageFamilyId = registration.Id;
            }
            try
            {
                var family = await _context.PackageFamilies.SingleAsync(
                    f => f.Id == package.PackageFamilyId, cancellationToken);
                if (!package.IsLocallyPublished && package.MirrorSource != null && family.IsLocallyPublished)
                    return PackageAddResult.PackageAlreadyExists;
                if (package.IsLocallyPublished)
                    family.IsLocallyPublished = true;
                // Include the family mode in the same optimistic write as the new version.
                // A mirror finishing after local publication must not commit its old mode.
                _context.Entry(family).Property(f => f.IsLocallyPublished).IsModified = true;
                _context.Packages.Add(package);

                await _context.SaveChangesAsync(cancellationToken);

                return PackageAddResult.Success;
            }
            catch (DbUpdateException)
            {
                return PackageAddResult.PackageAlreadyExists;
            }
        }

        public async Task<PackageFamily> GetOrCreateFamilyAsync(
            Package package, PackagePublisher publisher, CancellationToken cancellationToken)
        {
            if (!publisher.CanPublish && !publisher.CanManagePackages) return null;
            var normalizedId = package.Id.ToLowerInvariant();
            var registration = await _context.PackageFamilies
                .SingleOrDefaultAsync(r => r.NormalizedId == normalizedId, cancellationToken);
            if (registration != null)
                return Owns(registration, publisher) ? registration : null;

            registration = new PackageFamily
            {
                PackageId = package.Id, NormalizedId = normalizedId,
                OwnerUserId = publisher.UserId, CreatedAt = package.Published
            };
            _context.PackageFamilies.Add(registration);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return registration;
            }
            catch (DbUpdateException)
            {
                // Another publisher may have reserved the same name. Recheck the
                // winning owner before writing any package files.
                _context.Entry(registration).State = EntityState.Detached;
                var winner = await _context.PackageFamilies
                    .SingleOrDefaultAsync(r => r.NormalizedId == normalizedId, cancellationToken);
                if (winner == null) throw;
                return Owns(winner, publisher) ? winner : null;
            }
        }

        public async Task<bool> CanManagePackageAsync(string id, PackagePublisher publisher, CancellationToken cancellationToken)
        {
            if (publisher.CanManagePackages) return true;
            if (!publisher.CanPublish || publisher.UserId == null) return false;
            var normalizedId = id.ToLowerInvariant();
            return await _context.PackageFamilies.AnyAsync(
                r => r.NormalizedId == normalizedId && r.OwnerUserId == publisher.UserId, cancellationToken);
        }

        private static bool Owns(PackageFamily registration, PackagePublisher publisher) =>
            publisher.MirrorSource != null ? !registration.IsLocallyPublished && registration.OwnerUserId == null :
            publisher.CanManagePackages || (publisher.UserId != null && registration.OwnerUserId == publisher.UserId);

        public Task<bool> IsLocallyManagedAsync(string id, CancellationToken cancellationToken)
        {
            var normalizedId = id.ToLowerInvariant();
            return _context.PackageFamilies.AsNoTracking().AnyAsync(
                f => f.NormalizedId == normalizedId && (f.IsLocallyPublished || f.OwnerUserId != null), cancellationToken);
        }

        public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken)
        {
            return await _context
                .Packages
                .AsNoTracking()
                .Where(p => p.Id == id)
                .AnyAsync(cancellationToken);
        }

        public async Task<bool> ExistsAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            return await _context
                .Packages
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Where(p => p.NormalizedVersionString == version.ToNormalizedString())
                .AnyAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Package>> FindAsync(string id, bool includeUnlisted, CancellationToken cancellationToken)
        {
            var query = _context.Packages
                .AsNoTracking()
                .Include(p => p.Dependencies)
                .Include(p => p.PackageTypes)
                .Include(p => p.TargetFrameworks)
                .Where(p => p.Id == id);

            if (!includeUnlisted)
            {
                query = query.Where(p => p.Listed);
            }

            return (await query.ToListAsync(cancellationToken)).AsReadOnly();
        }

        public Task<Package> FindOrNullAsync(
            string id,
            NuGetVersion version,
            bool includeUnlisted,
            CancellationToken cancellationToken)
        {
            var query = _context.Packages
                .AsNoTracking()
                .Include(p => p.Dependencies)
                .Include(p => p.TargetFrameworks)
                .Where(p => p.Id == id)
                .Where(p => p.NormalizedVersionString == version.ToNormalizedString());

            if (!includeUnlisted)
            {
                query = query.Where(p => p.Listed);
            }

            return query.FirstOrDefaultAsync(cancellationToken);
        }

        public Task<bool> UnlistPackageAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            return TryUpdatePackageAsync(id, version, p => p.Listed = false, cancellationToken);
        }

        public Task<bool> RelistPackageAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            return TryUpdatePackageAsync(id, version, p => p.Listed = true, cancellationToken);
        }

        public async Task<bool> HardDeletePackageAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var package = await _context.Packages
                .Where(p => p.Id == id)
                .Where(p => p.NormalizedVersionString == version.ToNormalizedString())
                .Include(p => p.Dependencies)
                .Include(p => p.TargetFrameworks)
                .FirstOrDefaultAsync(cancellationToken);

            if (package == null)
            {
                return false;
            }

            _context.Packages.Remove(package);
            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }

        private async Task<bool> TryUpdatePackageAsync(
            string id,
            NuGetVersion version,
            Action<Package> action,
            CancellationToken cancellationToken)
        {
            var package = await _context.Packages
                .Where(p => p.Id == id)
                .Where(p => p.NormalizedVersionString == version.ToNormalizedString())
                .FirstOrDefaultAsync();

            if (package != null)
            {
                action(package);
                await _context.SaveChangesAsync(cancellationToken);

                return true;
            }

            return false;
        }
    }
}
