using Aiursoft.Canon.BackgroundJobs;
using Aiursoft.Nupkg.Services.SupplyChain;

namespace Aiursoft.Nupkg.Services.BackgroundJobs;

public class S360MirrorJob(ApprovedPackageMirror mirror, IHostApplicationLifetime lifetime,
    ILogger<S360MirrorJob> logger) : IBackgroundJob
{
    public string Name => "Prefetch S360-approved packages";
    public string Description => "Optionally preloads approved package files. Normal downloads fetch missing files on demand.";

    public async Task ExecuteAsync()
    {
        var count = await mirror.SynchronizeAsync(lifetime.ApplicationStopping);
        logger.LogInformation("S360 package synchronization completed: {Count} archives added or verified.", count);
    }
}
