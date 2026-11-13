using System.ComponentModel.DataAnnotations;

namespace Aiursoft.Nupkg.Core.Configuration;

public class S360MirrorOptions : IValidatableObject
{
    public bool Enabled { get; set; }
    // Policy identity is independent of the physical upstream. Empty preserves legacy admission.
    public string PolicySource { get; set; } = string.Empty;
    public bool PrefetchEnabled { get; set; }
    public Uri Server { get; set; }
    public string CheckKey { get; set; }
    [Range(1, 1440)] public int IntervalMinutes { get; set; } = 5;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled) yield break;
        if (Server == null || !Server.IsAbsoluteUri || Server.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(Server.UserInfo) || !string.IsNullOrEmpty(Server.Query) || !string.IsNullOrEmpty(Server.Fragment))
            yield return new ValidationResult("S360Mirror:Server must be an HTTPS server URL.");
        if (!string.IsNullOrEmpty(PolicySource) && (!Uri.TryCreate(PolicySource, UriKind.Absolute, out var policy) ||
            policy.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(policy.UserInfo) ||
            !string.IsNullOrEmpty(policy.Query) || !string.IsNullOrEmpty(policy.Fragment)))
            yield return new ValidationResult("S360Mirror:PolicySource must be the internal HTTPS NuGet index URL.");
        if (string.IsNullOrWhiteSpace(CheckKey))
            yield return new ValidationResult("S360Mirror:CheckKey is required when controlled mirroring is enabled.");
    }
}
