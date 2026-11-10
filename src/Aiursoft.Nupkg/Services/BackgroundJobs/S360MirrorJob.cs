using Aiursoft.Canon.BackgroundJobs;
using Aiursoft.Nupkg.Services.SupplyChain;

namespace Aiursoft.Nupkg.Services.BackgroundJobs;

public class S360MirrorJob(ApprovedPackageMirror mirror, IHostApplicationLifetime lifetime,
    ILogger<S360MirrorJob> logger) : IBackgroundJob
{
    public string Name => "S360 approved package mirror";
    public string Description => "Downloads independently verified, currently approved package versions from the configured upstream.";

    public async Task ExecuteAsync()
    {
        var count = await mirror.SynchronizeAsync(lifetime.ApplicationStopping);
        logger.LogInformation("S360 package synchronization completed: {Count} archives added or verified.", count);
    }
}
