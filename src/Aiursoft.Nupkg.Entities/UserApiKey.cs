using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Nupkg.Entities;

[Index(nameof(KeyHash), IsUnique = true)]
public class UserApiKey
{
    public int Id { get; set; }

    [Required] public required string UserId { get; set; }
    public User? User { get; set; }

    [Required, MaxLength(100)] public required string Name { get; set; }
    [Required, MaxLength(64)] public required string KeyHash { get; set; }
    [Required, MaxLength(8)] public required string KeyPrefix { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    [NotMapped] public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
}
