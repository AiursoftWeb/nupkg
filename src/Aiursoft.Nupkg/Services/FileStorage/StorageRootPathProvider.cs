using Aiursoft.Scanner.Abstractions;

namespace Aiursoft.Nupkg.Services.FileStorage;

public class StorageRootPathProvider(IConfiguration configuration) : ISingletonDependency
{
    public string GetStorageRootPath()
    {
        var path = configuration["Storage:Path"] ?? "/tmp/data";
        return path;
    }
}
