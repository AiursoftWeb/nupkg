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
Grant publishing permission to an account and create a personal key under **Personal > API Keys**. The first authorized publisher owns a new package name and its versions. Administrators can assign ownership of imported packages under **Packages > Package ownership**.

An existing nonempty `ApiKey` remains a server-wide legacy credential; set `EnableLegacyApiKey` to `false` after migrating automation to personal keys. Empty keys deny publishing by default. `AllowAnonymousPublishing=true` explicitly restores BaGet's anonymous-publishing behavior when the legacy key is empty. Package downloads remain public.

For an existing Nupkg database, grant the new **Publish Packages** and **Manage Packages** permissions through Roles. Imported package names remain reserved until an administrator assigns ownership.

## Package source

The NuGet v3 service index is `/v3/index.json`. Publishing uses `/api/v2/package`. Package files are stored under `Storage:Path`; use a persistent directory for this path and for the configured database.

## Credits

The application foundation comes from Aiursoft Template. The NuGet protocol, indexing, storage and package metadata implementation derives from BaGet. See `LICENSE`, `BAGET-LICENSE` and `THIRD-PARTY-NOTICES`.

## Upgrading BaGet

Follow the [migration guide](docs/migrate-from-baget.md) to retain your database, package files and feed address, then migrate automation to personal publishing keys.
