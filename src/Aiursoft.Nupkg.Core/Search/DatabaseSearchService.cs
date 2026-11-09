using Aiursoft.Nupkg.Entities;
using Aiursoft.Nupkg.Core.Indexing;
using Aiursoft.Nupkg.Core.Metadata;
using Aiursoft.Nupkg.Protocol.Models;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Nupkg.Core.Search
{
    public class DatabaseSearchService(
        NupkgDbContext context,
        IFrameworkCompatibilityService frameworks,
        ISearchResponseBuilder searchBuilder)
        : ISearchService
    {
        private readonly NupkgDbContext _context = context ?? throw new ArgumentNullException(nameof(context));
        private readonly IFrameworkCompatibilityService _frameworks = frameworks ?? throw new ArgumentNullException(nameof(frameworks));
        private readonly ISearchResponseBuilder _searchBuilder = searchBuilder ?? throw new ArgumentNullException(nameof(searchBuilder));

        public async Task<SearchResponse> SearchAsync(SearchRequest request,  CancellationToken cancellationToken)
        {
            var frameworks = GetCompatibleFrameworksOrNull(request.Framework);

            IQueryable<Package> search = _context.Packages.AsNoTracking();
            search = ApplySearchQuery(search, request.Query);
            search = ApplySearchFilters(
                search,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                request.PackageType,
                frameworks);

            var query = search.GroupBy(p => p.Id);
            var totalHits = await query.CountAsync(cancellationToken);

            var packageIds = await query
                .OrderByDescending(g => g.Max(p => p.Published))
                .Select(g => g.Key)
                .Skip(request.Skip)
                .Take(request.Take)
                .ToListAsync(cancellationToken);

            search = _context.Packages.AsNoTracking().Where(p => packageIds.Contains(p.Id));
            search = ApplySearchFilters(
                search,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                request.PackageType,
                frameworks);

            var results = await search.ToListAsync(cancellationToken);
            var groupedResults = results
                .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => new PackageRegistration(group.Key, group.ToList()))
                .OrderBy(registration => packageIds.IndexOf(registration.PackageId))
                .ToList();

            return _searchBuilder.BuildSearch(totalHits, groupedResults);
        }

        public async Task<AutocompleteResponse> AutocompleteAsync(
            AutocompleteRequest request,
            CancellationToken cancellationToken)
        {
            IQueryable<Package> search = _context.Packages.AsNoTracking();

            search = ApplySearchQuery(search, request.Query);
            search = ApplySearchFilters(
                search,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                request.PackageType,
                frameworks: null);

            var query = search.Select(p => p.Id).Distinct();
            var totalHits = await query.CountAsync(cancellationToken);

            var packageIds = await query
                .OrderBy(id => id)
                .Skip(request.Skip)
                .Take(request.Take)
                .ToListAsync(cancellationToken);

            return _searchBuilder.BuildAutocomplete(totalHits, packageIds);
        }

        public async Task<AutocompleteResponse> ListPackageVersionsAsync(
            VersionsRequest request,
            CancellationToken cancellationToken)
        {
            var packageId = request.PackageId.ToLower();
            var search = _context
                .Packages
                .AsNoTracking()
                .Where(p => p.Id.ToLower().Equals(packageId));

            search = ApplySearchFilters(
                search,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                packageType: null,
                frameworks: null);

            var packageVersions = await search
                .Select(p => p.NormalizedVersionString)
                .ToListAsync(cancellationToken);

            return _searchBuilder.BuildAutocomplete(packageVersions.Count, packageVersions);
        }

        public Task<DependentsResponse> FindDependentsAsync(string packageId, CancellationToken cancellationToken)
            => FindDependentsAsync(packageId, 0, 20, cancellationToken);

        public async Task<DependentsResponse> FindDependentsAsync(string packageId, int skip, int take, CancellationToken cancellationToken)
        {
            skip = Math.Max(0, skip);
            take = Math.Clamp(take, 1, 100);
            var normalizedId = packageId.ToLowerInvariant();
            // A name appears once, even when several listed versions depend on the target.
            var query = _context.PackageFamilies.AsNoTracking().Where(f => f.Versions.Any(p =>
                p.Listed && p.Dependencies.Any(d => d.Id != null && d.Id.ToLower() == normalizedId)));
            var total = await query.LongCountAsync(cancellationToken);
            var dependents = await query.OrderBy(f => f.NormalizedId).Skip(skip).Take(take)
                .Select(f => new PackageDependent
                {
                    Id = f.PackageId,
                    Description = f.Versions.Where(p => p.Listed)
                        .OrderByDescending(p => p.Published).Select(p => p.Description).FirstOrDefault()
                }).ToListAsync(cancellationToken);
            var response = _searchBuilder.BuildDependents(dependents);
            response.TotalHits = total;
            return response;
        }

        private IQueryable<Package> ApplySearchQuery(IQueryable<Package> query, string search)
        {
            if (string.IsNullOrEmpty(search))
            {
                return query;
            }

            search = search.ToLowerInvariant();

            return query.Where(p => p.Id.ToLower().Contains(search));
        }

        private IQueryable<Package> ApplySearchFilters(
            IQueryable<Package> query,
            bool includePrerelease,
            bool includeSemVer2,
            string packageType,
            IReadOnlyList<string> frameworks)
        {
            if (!includePrerelease)
            {
                query = query.Where(p => !p.IsPrerelease);
            }

            if (!includeSemVer2)
            {
                query = query.Where(p => p.SemVerLevel != SemVerLevel.SemVer2);
            }

            if (!string.IsNullOrEmpty(packageType))
            {
                query = query.Where(p => p.PackageTypes.Any(t => t.Name == packageType));
            }

            if (frameworks != null)
            {
                query = query.Where(p => p.TargetFrameworks.Any(f => frameworks.Contains(f.Moniker)));
            }

            return query.Where(p => p.Listed);
        }

        private IReadOnlyList<string> GetCompatibleFrameworksOrNull(string framework)
        {
            if (framework == null) return null;

            return _frameworks.FindAllCompatibleFrameworks(framework);
        }
    }
}
