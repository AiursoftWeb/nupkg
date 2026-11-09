# Move from BaGet to Nupkg

Nupkg keeps BaGet's NuGet v3 routes, publishing endpoints, package records and package/symbol storage layout. Keep the existing public feed address so clients do not need a new source.

## 1. Prepare a recovery point

Stop BaGet before taking a consistent backup of its database and storage root. Keep the previous application image and configuration with that backup. Keep new publishing paused until the upgrade has been checked.

Use the same database provider as before: `Sqlite` or `MySql`. This upgrade does not convert one provider into the other. Test a copy of your own database and files first.

## 2. Point Nupkg at the existing data

Set these values using the existing configuration or environment-variable mechanism:

- `ConnectionStrings:DbType`: the existing provider.
- `ConnectionStrings:DefaultConnection`: the existing database connection.
- `Storage:Path`: the existing storage root, which contains `packages` and `symbols`.
- `ApiKey`: the existing nonempty server key, while migrating publishing clients.
- `PathBase`: the existing URL prefix, if the feed is hosted below the domain root.

Do not move NuGet files into `Workspace` or `Vault`. Those are separate Template file areas that Nupkg can add under the same root. Do not run BaGet and Nupkg against the same live database at the same time.

Start Nupkg and allow its normal startup migrations to finish. Existing versions are grouped into package names. Imported names start **Unassigned**, so an ordinary publisher cannot claim an existing name by uploading another version.

Configure the intended Local or OIDC sign-in and replace the bootstrap administrator credentials before exposing administration. The local development credentials are listed in the main README.

## 3. Check the feed before accepting new writes

Check `/health` and `/v3/index.json` at the feed's public address, including its URL prefix. Use a fresh NuGet cache to restore an existing package and its dependencies. Also check an explicitly selected unlisted version, a prerelease, a symbol download, and any tool or template packages you use.

If publishing was previously allowed, try a new version with the retained server key. Check the settings you actually use, including `IsReadOnlyMode`, `AllowPackageOverwrites`, `PackageDeletionBehavior`, and `Mirror`.

Downloads remain public. Unlisting removes a version from search; it does not make its exact package URL private.

## 4. Move publishing to personal keys

Grant **Publish Packages** to publishers. Grant **Manage Packages** only to accounts that should manage every package and assign ownership. For an existing Nupkg account database, administrators must grant these new permissions through Roles.

Assign imported names under **Packages > Package ownership**. Create a personal key under **Personal > API Keys**, save the one-time secret, and replace the corresponding key in automation. Test the owner's package and symbol uploads before revoking the previous credential.

A nonempty legacy `ApiKey` has server-wide authority, including names assigned to personal owners. Set `EnableLegacyApiKey=false` after all publishing clients have moved.

An empty `ApiKey` denies anonymous publishing by default. Only set `AllowAnonymousPublishing=true` when deliberately restoring BaGet's old empty-key behavior. It requires legacy authentication to remain enabled and the legacy key to be empty; anyone can then modify packages.

## Recovery

Stop Nupkg, restore the paired database/storage backup and previous configuration, and start the previous BaGet image. Account for any packages published after the backup before reverting; restoring a backup loses later database changes.

Downgrading the personal-publishing migration removes personal keys and ownership information. A database downgrade is not a substitute for the paired recovery backup.

## Template clients

The package page defaults to `dotnet new install Package@Version` for .NET SDK 9.0.200 and later. Select **Earlier SDKs** for the previous `Package::Version` syntax on SDKs supporting `dotnet new install`. .NET 6 and earlier use `dotnet new --install Package::Version`. See the [official template installation documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new-install).
