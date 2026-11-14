# Nupkg

[![MIT licensed](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/aiursoftweb/nupkg/blob/master/LICENSE)
[![Pipeline stat](https://badge-proxy.aiursoft.com/aiursoft/nupkg/badges/master/pipeline.svg)](https://gitlab.aiursoft.com/aiursoft/nupkg/-/pipelines)
[![Test Coverage](https://badge-proxy.aiursoft.com/aiursoft/nupkg/badges/master/coverage.svg)](https://gitlab.aiursoft.com/aiursoft/nupkg/-/pipelines)
[![S360 compliance](https://s360.aiursoft.com/badge.svg?project=aiursoft%2Fnupkg)](https://s360.aiursoft.com/SupplyChain?tab=projects&project=aiursoft%2Fnupkg&scope=baseline&ecosystem=all)
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
- Optional S360 admission using a single internal-source policy, using package rules and known vulnerability data.
- Shared Aiursoft accounts, OIDC, roles, themes and localization.

Downloads are public. **Publish Packages** allows an account to create personal keys and maintain its own package names. **Manage Packages** allows maintaining all packages and assigning ownership. Imported and mirrored names remain reserved until assigned by an administrator.

The npm sibling is [Nopkg](https://github.com/aiursoftweb/nopkg).

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

With S360 control disabled, to cache an upstream feed, set `Mirror:Enabled=true` and `Mirror:PackageSource=https://api.nuget.org/v3/index.json`. Packages are cached when requested; this does not copy the entire upstream catalog.

For approved-only on-demand mirroring, deploy S360 with its artifact admission API first, then configure Nupkg:

```text
Mirror__Enabled=true
Mirror__PackageSource=https://api.nuget.org/v3/index.json
S360Mirror__Enabled=true
S360Mirror__Server=https://s360.aiursoft.com
S360Mirror__CheckKey=<personal S360 check key>
S360Mirror__PrefetchEnabled=false
S360Mirror__IntervalMinutes=5
```

Keep the key outside Git; baseline permission is unnecessary. Set `S360Mirror__PolicySource` to this registry's URL (for example, `https://packages.example.com/v3/index.json`) and permit that URL in S360's `SupplyChain:NuGetSources`. Approve each package name and version range there once. Leave `PolicySource` empty to use the upstream URL as the policy identity. **S360 does not need an upstream mapping and never downloads package files.**

Nupkg asks the existing `POST /api/artifacts/assess` API whether each name and version is allowed by policy and current vulnerability data. It sends no archive or file hash. Version discovery uses upstream metadata; an approved version is downloaded only when first requested. Nupkg checks the archive identity and records its own SHA256 before committing the cache, then checks policy again. Concurrent requests for one version share a download. Run one application instance per filesystem package store.

Search, version metadata and cached mirror downloads use current S360 decisions. Revoked mirror versions cannot be downloaded, including through direct cached URLs. S360 failures or unavailable vulnerability data return 503 rather than falling back to unrestricted mirroring. Package responses use `Cache-Control: no-store`; reverse proxies must respect it. Previously downloaded client files cannot be revoked remotely.

**Local publishing is separate:** an authorized upload permanently makes that package name locally managed. Local packages bypass mirror admission, and missing versions are never fetched upstream. Project baseline reports and vulnerability alerts remain independent of package downloads.

The Jobs page can prefetch approved, previously reported concrete versions. Enable scheduled prefetch with `S360Mirror__PrefetchEnabled=true`; `IntervalMinutes` defaults to 5. Prefetch is optional and disabled by default. It uses the same policy checks and download locks as lazy requests, without overwriting existing conflicting archives or local package names. NuGet v2 upstreams are not supported in controlled mode.

Cached packages and metadata remain usable with the upstream offline while S360 remains reachable and approves them. Prepare the full dependency closure before disconnecting the upstream. `Mirror__Enabled=false` pauses new downloads while retaining S360 read checks; disabling `S360Mirror__Enabled` disables those checks.

Existing databases and archives are retained. Legacy ownerless cache entries are adopted after Nupkg checks their package identity and current policy; their locally recorded provenance is distinct from S360's policy decision. Nupkg does not create approval rules or publish project baselines.

## Credits

The application foundation comes from [Aiursoft Template](https://github.com/aiursoftweb/template). The NuGet protocol, indexing, storage and package metadata implementation derives from BaGet. See `LICENSE`, `BAGET-LICENSE` and `THIRD-PARTY-NOTICES`.
