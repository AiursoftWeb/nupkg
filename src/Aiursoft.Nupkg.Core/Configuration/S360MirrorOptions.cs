using System.ComponentModel.DataAnnotations;

namespace Aiursoft.Nupkg.Core.Configuration;

public class S360MirrorOptions : IValidatableObject
{
    public bool Enabled { get; set; }
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
        if (string.IsNullOrWhiteSpace(CheckKey))
            yield return new ValidationResult("S360Mirror:CheckKey is required when controlled mirroring is enabled.");
    }
}
