using Aiursoft.Nupkg.Core.Metadata;
using Aiursoft.Nupkg.Protocol.Models;

namespace Aiursoft.Nupkg.Core.Search
{
    public interface ISearchResponseBuilder
    {
        SearchResponse BuildSearch(long totalHits, IReadOnlyList<PackageRegistration> results);
        AutocompleteResponse BuildAutocomplete(long totalHits, IReadOnlyList<string> data);
        DependentsResponse BuildDependents(IReadOnlyList<PackageDependent> results);
    }
}
