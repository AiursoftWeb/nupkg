# Nupkg.Protocol

[![Build Status](https://sharml.visualstudio.com/Nupkg/_apis/build/status/loic-sharma.Nupkg)](https://sharml.visualstudio.com/Nupkg/_build/latest?definitionId=2) [![Join the chat at https://gitter.im/NupkgServer/community](https://badges.gitter.im/NupkgServer/community.svg)](https://gitter.im/NupkgServer/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge) [![Nuget](https://img.shields.io/nuget/dt/Nupkg.Protocol)](https://www.nuget.org/packages/Nupkg.Protocol)

Nupkg's SDK to interact with the [NuGet protocol](https://docs.microsoft.com/en-us/nuget/api/overview).

[Package (NuGet)](https://www.nuget.org/packages/Nupkg.Protocol) | [Documentation](https://loic-sharma.github.io/Nupkg/advanced/sdk/) | [Samples](https://github.com/loic-sharma/Nupkg/tree/master/samples)

## Getting started

### Install the package

```powershell
dotnet add package Nupkg.Protocol
```

## Examples

### List Package Versions

Find all versions of the `Newtonsoft.Json` package:

```csharp
NuGetClient client = new NuGetClient("https://api.nuget.org/v3/index.json");

IReadOnlyList<NuGetVersion>> versions = await client.ListPackageVersionsAsync("Newtonsoft.Json");

foreach (NuGetVersion version in versions)
{
    Console.WriteLine($"Found version: {version}");
}
```

### Download a package

```csharp
NuGetClient client = new NuGetClient("https://api.nuget.org/v3/index.json");

string packageId = "Newtonsoft.Json";
NuGetVersion packageVersion = new NuGetVersion("12.0.1");

using (Stream packageStream = await client.DownloadPackageAsync(packageId, packageVersion))
{
    Console.WriteLine($"Downloaded package {packageId} {packageVersion}");
}
```

### Find Package Metadata

```csharp
NuGetClient client = new NuGetClient("https://api.nuget.org/v3/index.json");

// Find the metadata for all versions of a package.
IReadOnlyList<PackageMetadata> items = await client.GetPackageMetadataAsync("Newtonsoft.Json");
if (!items.Any())
{
    Console.WriteLine($"Package 'Newtonsoft.Json' does not exist");
    return;
}

foreach (var metadata in items)
{
    Console.WriteLine($"Version: {metadata.Version}");
    Console.WriteLine($"Listed: {metadata.Listed}");
    Console.WriteLine($"Tags: {metadata.Tags}");
    Console.WriteLine($"Description: {metadata.Description}");
}

// Or, find the metadata for a single version of a package.
string packageId = "Newtonsoft.Json"
NuGetVersion packageVersion = new NuGetVersion("12.0.1");

PackageMetadata metadata = await client.GetPackageMetadataAsync(packageId, packageVersion);

Console.WriteLine($"Listed: {metadata.Listed}");
Console.WriteLine($"Tags: {metadata.Tags}");
Console.WriteLine($"Description: {metadata.Description}");
```

### Search for packages

Search for "json" packages:

```csharp
NuGetClient client = new NuGetClient("https://api.nuget.org/v3/index.json");
IReadOnlyList<SearchResult> results = await client.SearchAsync("json");

foreach (SearchResult result in results)
{
    Console.WriteLine($"Found package {result.PackageId} {searchResult.Version}");
}
```
