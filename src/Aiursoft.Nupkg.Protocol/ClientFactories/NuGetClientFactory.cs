using Aiursoft.Nupkg.Protocol.Catalog;
using Aiursoft.Nupkg.Protocol.Models;
using Aiursoft.Nupkg.Protocol.PackageContent;
using Aiursoft.Nupkg.Protocol.PackageMetadata;
using Aiursoft.Nupkg.Protocol.Search;

namespace Aiursoft.Nupkg.Protocol.ClientFactories
{
    public class NuGetClients
    {
        public ServiceIndexResponse ServiceIndex { get; set; }
        public IPackageContentClient PackageContentClient { get; set; }
        public IPackageMetadataClient PackageMetadataClient { get; set; }
        public ISearchClient SearchClient { get; set; }
        public IAutocompleteClient AutocompleteClient { get; set; }
        public ICatalogClient CatalogClient { get; set; }
    }
}
