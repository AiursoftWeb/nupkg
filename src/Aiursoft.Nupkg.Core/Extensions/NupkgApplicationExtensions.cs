using Aiursoft.Nupkg.Core.Configuration;
using Aiursoft.Nupkg.Core.Search;
using Aiursoft.Nupkg.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aiursoft.Nupkg.Core.Extensions
{
    public static class NupkgApplicationExtensions
    {
        public static NupkgApplication AddFileStorage(this NupkgApplication app)
        {
            app.Services.TryAddTransient<IStorageService>(provider => provider.GetRequiredService<FileStorageService>());
            return app;
        }

        public static NupkgApplication AddFileStorage(
            this NupkgApplication app,
            Action<FileSystemStorageOptions> configure)
        {
            app.AddFileStorage();
            app.Services.Configure(configure);
            return app;
        }

        public static NupkgApplication AddNullStorage(this NupkgApplication app)
        {
            app.Services.TryAddTransient<IStorageService>(provider => provider.GetRequiredService<NullStorageService>());
            return app;
        }

        public static NupkgApplication AddNullSearch(this NupkgApplication app)
        {
            app.Services.TryAddTransient<ISearchIndexer>(provider => provider.GetRequiredService<NullSearchIndexer>());
            app.Services.TryAddTransient<ISearchService>(provider => provider.GetRequiredService<NullSearchService>());
            return app;
        }
    }
}
