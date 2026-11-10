namespace Aiursoft.Nupkg.Services.SupplyChain;

/// <summary>Bounded, process-wide locks shared by requests and optional prefetch jobs.</summary>
public class PackageMirrorLocks
{
    private readonly SemaphoreSlim[] _locks = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public SemaphoreSlim For(string id, string normalizedVersion) =>
        _locks[(uint)StringComparer.OrdinalIgnoreCase.GetHashCode(id + "/" + normalizedVersion) % _locks.Length];
}
