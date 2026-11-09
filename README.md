# Nupkg

[![MIT licensed](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/aiursoftweb/nupkg/blob/master/LICENSE)
[![Pipeline stat](https://badge-proxy.aiursoft.com/aiursoft/nupkg/badges/master/pipeline.svg)](https://gitlab.aiursoft.com/aiursoft/nupkg/-/pipelines)
[![Test Coverage](https://badge-proxy.aiursoft.com/aiursoft/nupkg/badges/master/coverage.svg)](https://gitlab.aiursoft.com/aiursoft/nupkg/-/pipelines)
[![Man hours](https://manhours.aiursoft.com/r/github.com/aiursoftweb/nupkg.svg)](https://manhours.aiursoft.com/r/github.com/aiursoftweb/nupkg.html)
[![Website](https://img.shields.io/website?url=https%3A%2F%2Fnupkg.aiursoft.com)](https://nupkg.aiursoft.com)
[![Docker](https://img.shields.io/docker/pulls/aiursoft/nupkg.svg)](https://hub.docker.com/r/aiursoft/nupkg)

Nupkg is a NuGet package registry built on Aiursoft Template. Publish your packages, browse versions and dependencies, and cache packages from an upstream feed using standard NuGet tools.

![screenshot](./screenshot.png)

## Try

Browse the running [Nupkg instance](https://nupkg.aiursoft.com).

## What is Nupkg?

- A NuGet v3 feed with package and symbol publishing support.
- A package catalog with versions, dependencies, reverse dependencies and install commands for libraries, tools and templates.
- Personal publishing keys with expiration, revocation and package ownership.
- Optional on-demand upstream mirroring: requested packages are downloaded and cached locally.
- Shared Aiursoft accounts, OIDC, roles, themes and localization.

Downloads are public. **Publish Packages** allows an account to create personal keys and maintain its own package names. **Manage Packages** allows maintaining all packages and assigning ownership. Imported and mirrored names remain reserved until assigned by an administrator.

## Use the feed

Add your server as a package source:

```sh
dotnet nuget add source https://nupkg.example.com/v3/index.json --name Nupkg
dotnet add package Your.Package --source https://nupkg.example.com/v3/index.json
```

Create a personal key under **Personal > API Keys**, then publish:

```sh
dotnet nuget push ./Your.Package.1.0.0.nupkg \
  --source https://nupkg.example.com/v3/index.json \
  --api-key YOUR_PERSONAL_KEY
```

The first authorized publisher owns a new package name and its versions. Manage imported names under **Packages > Package ownership**.

## Run locally

Requires .NET 10 and Node.js.

```sh
cd src/Aiursoft.Nupkg/wwwroot
npm ci
cd ..
dotnet run
```

The default local administrator is `admin@default.com` with password `Admin@123456!`. Configure authentication and roles before exposing a deployment.

## Deploy

Use the [Docker image](https://hub.docker.com/r/aiursoft/nupkg) or the repository installer:

```sh
curl -fsSL https://raw.githubusercontent.com/aiursoftweb/nupkg/master/install.sh | sudo bash -s -- 8080
```

Persist `/data` for the default SQLite database and package files. For other databases, persist the configured database and `Storage:Path`. The NuGet v3 service index is `/v3/index.json`; publishing uses `/api/v2/package`; health checks use `/health`.

To cache an upstream feed, set `Mirror:Enabled=true` and `Mirror:PackageSource=https://api.nuget.org/v3/index.json`. Packages are cached when requested; this does not copy the entire upstream catalog.

A nonempty `ApiKey` can provide a server-wide legacy credential. Set `EnableLegacyApiKey=false` to use personal keys only. Empty keys deny publishing by default; `AllowAnonymousPublishing=true` explicitly enables anonymous publishing when the legacy key is empty. For existing installations, grant the **Publish Packages** and **Manage Packages** permissions through Roles.

Moving an existing BaGet installation is optional. See the [migration guide](docs/migrate-from-baget.md) for compatibility and backup steps.

## Credits

The application foundation comes from [Aiursoft Template](https://github.com/aiursoftweb/template). The NuGet protocol, indexing, storage and package metadata implementation derives from BaGet. See `LICENSE`, `BAGET-LICENSE` and `THIRD-PARTY-NOTICES`.
