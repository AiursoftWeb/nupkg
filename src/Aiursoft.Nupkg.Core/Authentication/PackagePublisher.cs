namespace Aiursoft.Nupkg.Core.Authentication;

/// <summary>
/// Publishing authority resolved by the host. Internal indexing and an explicitly
/// configured legacy credential have server-wide authority without a user owner.
/// </summary>
public sealed record PackagePublisher(string UserId, bool CanPublish, bool CanManagePackages)
{
    public static PackagePublisher System { get; } = new(null, true, true);
}
