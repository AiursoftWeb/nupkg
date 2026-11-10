using Aiursoft.Nupkg.Core;
using Aiursoft.Nupkg.Core.Extensions;
using Aiursoft.Nupkg.Core.Search;
using Aiursoft.Nupkg.Core.Storage;
using Aiursoft.Nupkg.Services;
using Aiursoft.Nupkg.Extensions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Aiursoft.CSTools.Tools;
using Aiursoft.Canon.TaskQueue;
using Aiursoft.Canon.BackgroundJobs;
using Aiursoft.Canon.ScheduledTasks;
using Aiursoft.DbTools.Switchable;
using Aiursoft.Scanner;
using Aiursoft.Nupkg.Configuration;
using Aiursoft.WebTools.Abstractions.Models;
using Aiursoft.Nupkg.InMemory;
using Aiursoft.Nupkg.MySql;
using Aiursoft.Nupkg.Services.Authentication;
using Aiursoft.Nupkg.Services.BackgroundJobs;
using Aiursoft.Nupkg.Sqlite;
using Aiursoft.UiStack;
using Aiursoft.UiStack.Layout;
using Aiursoft.UiStack.Navigation;
using Microsoft.AspNetCore.Mvc.Razor;
using Aiursoft.ClickhouseLoggerProvider;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Diagnostics.CodeAnalysis;
using EFCoreSecondLevelCacheInterceptor;

namespace Aiursoft.Nupkg;

[ExcludeFromCodeCoverage]
public class Startup : IWebStartup
{
    public void ConfigureServices(IConfiguration configuration, IWebHostEnvironment environment, IServiceCollection services)
    {
        var controlledMirror = configuration.GetSection("S360Mirror").Get<Core.Configuration.S360MirrorOptions>() ?? new();
        services.AddOptions<Core.Configuration.S360MirrorOptions>().ValidateOnStart();
        services.AddScoped<Services.SupplyChain.S360AdmissionClient>();
        services.AddScoped<Services.SupplyChain.ApprovedPackageMirror>();
        services.AddHttpClient("S360Admission", client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        // AppSettings.
        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));

        // Relational database
        var (connectionString, dbType, allowCache) = configuration.GetDbSettings();
        if (allowCache)
        {
            // Register before DbTools supplies its default cache settings. Retain its
            // package-query policy, but always read publishing authority from the database.
            services.AddEFSecondLevelCache(options =>
            {
                options.UseMemoryCacheProvider().ConfigureLogging(enable: false);
                options.CacheAllQueries(CacheExpirationMode.Sliding, TimeSpan.FromMinutes(30));
                options.UseDbCallsIfCachingProviderIsDown(TimeSpan.FromMinutes(1));
                options.SkipCachingCommands(command =>
                    controlledMirror.Enabled || command.Contains("AspNet", StringComparison.OrdinalIgnoreCase)
                    || command.Contains("UserApiKeys", StringComparison.OrdinalIgnoreCase)
                    || command.Contains("PackageFamilies", StringComparison.OrdinalIgnoreCase));
            });
        }
        services.AddSwitchableRelationalDatabase(
            dbType: EntryExtends.IsInUnitTests() ? "InMemory" : dbType,
            connectionString: connectionString,
            supportedDbs:
            [
                new MySqlSupportedDb(allowCache: allowCache, splitQuery: false),
                new SqliteSupportedDb(allowCache: allowCache, splitQuery: true),
                new InMemorySupportedDb()
            ]);

        services.AddLogging(builder =>
        {
            builder.AddClickhouse(options => configuration.GetSection("Logging:Clickhouse").Bind(options));
        });

        // Authentication and Authorization
        services.AddTemplateAuth(configuration);
        services.AddScoped<PackagePublisherAuthenticationService>();

        // Services
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddAssemblyDependencies(typeof(Startup).Assembly);
        services.AddSingleton<NavigationState<Startup>>();

        // Background job infrastructure
        services.AddTaskQueueEngine();
        services.AddScheduledTaskEngine();

        // Background jobs
        services.RegisterBackgroundJob<DummyJob>();
        var orphanAvatarCleanupJob = services.RegisterBackgroundJob<OrphanAvatarCleanupJob>();

        // Scheduled tasks (attach a schedule to any registered background job)
        services.RegisterScheduledTask(
            registration: orphanAvatarCleanupJob,
            period:     TimeSpan.FromHours(6),
            startDelay: TimeSpan.FromMinutes(5));

        if (controlledMirror.Enabled)
        {
            var mirror = services.RegisterBackgroundJob<S360MirrorJob>();
            services.RegisterScheduledTask(mirror, TimeSpan.FromMinutes(Math.Clamp(controlledMirror.IntervalMinutes, 1, 1440)),
                TimeSpan.FromSeconds(30));
        }

        // NuGet services retain their protocol and package storage contracts.
        services.AddHttpContextAccessor();
        services.AddTransient<IUrlGenerator, NupkgUrlGenerator>();
        services.AddTransient<IConfigureOptions<CorsOptions>, ConfigureNupkgOptions>();
        services.AddTransient<IConfigureOptions<FormOptions>, ConfigureNupkgOptions>();
        services.AddTransient<IValidateOptions<Core.Configuration.NupkgOptions>, ConfigureNupkgOptions>();
        services.AddConfiguration();
        services.AddNupkgServices();
        services.AddDefaultProviders();
        new NupkgApplication(services).AddFileStorage();
        services.AddProvider<ISearchIndexer>((provider, config) =>
            !config.HasSearchType(DependencyInjectionExtensions.DatabaseSearchType) ? null! : provider.GetRequiredService<NullSearchIndexer>());
        services.AddProvider<ISearchService>((provider, config) =>
            !config.HasSearchType(DependencyInjectionExtensions.DatabaseSearchType) ? null! : provider.GetRequiredService<DatabaseSearchService>());
        services.AddFallbackServices();
        services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<IStorageService>);
        services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<ISearchService>);
        services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<ISearchIndexer>);
        services.AddTransient<PackageDatabase>();
        services.AddCors();

        // Controllers and localization
        services.AddControllersWithViews(options => options.OutputFormatters.Insert(0, new NuGetJsonOutputFormatter()))
            .AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
                options.SerializerSettings.ContractResolver = new DefaultContractResolver();
            })
            .AddApplicationPart(typeof(Startup).Assembly)
            .AddApplicationPart(typeof(UiStackLayoutViewModel).Assembly)
            .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
            .AddDataAnnotationsLocalization();
    }

    public void Configure(WebApplication app)
    {
        app.UsePathBase(app.Configuration["PathBase"] ?? string.Empty);
        app.UseWhen(context => !context.Request.Path.StartsWithSegments("/v3")
            && !context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/health"), ui =>
        {
            ui.UseExceptionHandler("/Error/Code500");
            ui.UseStatusCodePagesWithReExecute("/Error/Code{0}");
        });
        app.UseUIStack();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseCors(ConfigureNupkgOptions.CorsPolicy);
        app.UseOperationCancelledMiddleware();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<Services.SupplyChain.MirrorAdmissionMiddleware>();
        app.MapDefaultControllerRoute();
        app.MapControllers();
    }
}
