# Aiursoft Nupkg

A NuGet package server built on Aiursoft Template. Browse packages, inspect versions and dependencies, and publish with the NuGet CLI. The web application uses the shared UIStack, local/OIDC accounts, permissions, themes, and localization.

## Run locally

Requires .NET 10 and Node.js.

```sh
cd src/Aiursoft.Nupkg/wwwroot
npm ci
cd ..
dotnet run
```

The default local administrator is `admin@default.com` with password `Admin@123456!`.
Configure `ApiKey` before publishing packages. Browser accounts and the NuGet publishing API key serve separate purposes.

## Package source

The NuGet v3 service index is `/v3/index.json`. Publishing uses `/api/v2/package`. Package files are stored under `Storage:Path`; use a persistent directory for this path and for the configured database.

## Credits

The application foundation comes from Aiursoft Template. The NuGet protocol, indexing, storage and package metadata implementation derives from BaGet. See `LICENSE`, `BAGET-LICENSE` and `THIRD-PARTY-NOTICES`.
