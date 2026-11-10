using Aiursoft.Nupkg.Controllers;
using Aiursoft.Nupkg.Controllers.Protocol;
using Aiursoft.Nupkg.Core.Configuration;
using Aiursoft.Nupkg.Entities;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace Aiursoft.Nupkg.Services.SupplyChain;

public class MirrorAdmissionMiddleware(RequestDelegate next, ILogger<MirrorAdmissionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, NupkgDbContext db,
        S360AdmissionClient admission, IOptions<S360MirrorOptions> options,
        IOptions<MirrorOptions> mirrorOptions)
    {
        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        var packageRead = action != null && ((action.ControllerTypeInfo == typeof(CatalogController) && action.ActionName != nameof(CatalogController.Upload)) ||
            (action.ControllerTypeInfo.Namespace == typeof(SymbolController).Namespace &&
                action.ControllerTypeInfo != typeof(HealthController) && action.ControllerTypeInfo != typeof(ServiceIndexController)));
        if (!options.Value.Enabled || !packageRead ||
            !(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)))
        {
            await next(context);
            return;
        }
        // No cached policy decisions. Query filters cover catalog/search/registration and
        // archives already on disk as well as nuspec, readme and icon requests.
        db.RestrictMirrorReads = true;
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            if (mirrorOptions.Value.Enabled &&
                action!.ControllerTypeInfo == typeof(PackageContentController) &&
                context.Request.RouteValues["id"] is string requestedName &&
                context.Request.RouteValues["version"] is string requestedVersion &&
                NuGetVersion.TryParse(requestedVersion, out var parsedVersion))
            {
                var mirror = context.RequestServices.GetRequiredService<ApprovedPackageMirror>();
                var result = await mirror.EnsureAsync(requestedName, parsedVersion, context.RequestAborted);
                if (result is PackageMirrorResult.Denied or PackageMirrorResult.Unavailable)
                {
                    context.Response.StatusCode = result == PackageMirrorResult.Denied
                        ? StatusCodes.Status403Forbidden : StatusCodes.Status502BadGateway;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = result == PackageMirrorResult.Denied
                            ? "This package version is not permitted for upstream mirroring."
                            : "The approved package could not be downloaded and verified. Please retry."
                    });
                    return;
                }
            }
            // Exact package endpoints need only this name; catalog and search assess the
            // mirrored catalog so related packages and aggregate counts stay consistent.
            var requestedId = action!.ControllerTypeInfo == typeof(PackageContentController) ||
                action.ControllerTypeInfo == typeof(PackageMetadataController)
                ? context.Request.RouteValues["id"]?.ToString()?.ToLowerInvariant() : null;
            var mirrors = await db.Packages.IgnoreQueryFilters().AsNoTracking()
                .Where(p => requestedId == null || p.Id.ToLower() == requestedId)
                .Where(p => !p.IsLocallyPublished && p.UploadedByUserId == null && p.MirrorSource != null &&
                    p.MirrorVersion != null && p.MirrorSha256 != null)
                .Select(p => new { p.Key, p.Id, p.MirrorSource, p.MirrorVersion, p.MirrorSha256 })
                .ToListAsync(context.RequestAborted);
            var identities = mirrors.Select(p => new ArtifactIdentity("nuget", p.Id, p.MirrorVersion!,
                p.MirrorSource!, p.MirrorSha256!)).ToList();
            var decisions = await admission.AssessAsync(identities, context.RequestAborted);
            db.ApprovedMirrorKeys = mirrors.Where((_, i) => decisions[i].Trusted).Select(p => p.Key).ToArray();
            if (action.ControllerTypeInfo == typeof(PackageContentController) &&
                context.Request.RouteValues["id"] is string id &&
                context.Request.RouteValues["version"] is string versionString &&
                NuGetVersion.TryParse(versionString, out var version))
            {
                var normalized = version.ToNormalizedString().ToLowerInvariant();
                var denied = await db.Packages.IgnoreQueryFilters().AsNoTracking().Where(p =>
                    p.Id.ToLower() == id.ToLower() && p.NormalizedVersionString == normalized &&
                    !p.IsLocallyPublished && p.UploadedByUserId == null && !db.ApprovedMirrorKeys.Contains(p.Key))
                    .Select(p => p.Key).FirstOrDefaultAsync(context.RequestAborted);
                if (denied != 0)
                {
                    var index = mirrors.FindIndex(p => p.Key == denied);
                    var reason = index < 0 ? "Legacy archive has not been verified for controlled mirroring." : decisions[index].Reason;
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { error = reason });
                    return;
                }
            }
            await next(context);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or System.Text.Json.JsonException or TaskCanceledException)
        {
            if (context.RequestAborted.IsCancellationRequested || context.Response.HasStarted) throw;
            logger.LogWarning("S360 admission is unavailable ({FailureType}); mirror reads were denied.", ex.GetType().Name);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = "30";
            await context.Response.WriteAsJsonAsync(new { error = "Package admission is temporarily unavailable. Please retry." });
        }
    }
}
