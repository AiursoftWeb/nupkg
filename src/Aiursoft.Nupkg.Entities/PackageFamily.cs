using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Nupkg.Entities;

/// <summary>
/// A package name and its owner, shared by every version. An unassigned legacy
/// name remains reserved even when all its versions have been removed.
/// </summary>
[Index(nameof(NormalizedId), IsUnique = true)]
public class PackageFamily
{
    public int Id { get; set; }

    [Required, MaxLength(NupkgDbContext.MaxPackageIdLength)]
    public required string PackageId { get; set; }

    [Required, MaxLength(NupkgDbContext.MaxPackageIdLength)]
    public required string NormalizedId { get; set; }

    public string? OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Package> Versions { get; set; } = [];
}
