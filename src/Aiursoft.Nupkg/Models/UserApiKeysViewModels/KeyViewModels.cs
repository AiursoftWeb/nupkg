using System.ComponentModel.DataAnnotations;
using Aiursoft.Nupkg.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.Nupkg.Models.UserApiKeysViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public List<UserApiKey> Keys { get; set; } = [];
}

public class CreateViewModel : UiStackLayoutViewModel
{
    public const int MaxExpirationDays = 36500;

    [Required, MaxLength(100)]
    [Display(Name = "Friendly Name")]
    public string Name { get; set; } = string.Empty;

    [Range(1, MaxExpirationDays)]
    [Display(Name = "Expiration")]
    public int ExpirationDays { get; set; } = 365;
}

public class UsageViewModel : UiStackLayoutViewModel
{
    public required UserApiKey Key { get; set; }
    public string? RawKey { get; set; }
    public string ServiceIndexUrl { get; set; } = string.Empty;
    public bool CanPublish { get; set; }
}

public class DeleteViewModel : UiStackLayoutViewModel
{
    public required UserApiKey Key { get; set; }
}

public class EditViewModel : UiStackLayoutViewModel
{
    public int Id { get; set; }
    public string KeyPrefix { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public long TotalUses { get; set; }

    [Required, MaxLength(100)]
    [Display(Name = "Friendly Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Expiration Time")]
    public DateTime? ExpiresAt { get; set; }
}
