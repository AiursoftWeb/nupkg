using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Aiursoft.Nupkg.Entities;

/// <summary>
/// A package name and its owner, shared by every version. An unassigned legacy
/// name remains reserved even when all its versions have been removed.
/// </summary>
[Index(nameof(NormalizedId), IsUnique = true)]
public class PackageFamily
{
    [Key]
    public int Id { get; init; }

    [Required, MaxLength(NupkgDbContext.MaxPackageIdLength)]
    public required string PackageId { get; init; }

    [Required, MaxLength(NupkgDbContext.MaxPackageIdLength)]
    public required string NormalizedId { get; init; }

    /// <summary>
    /// Null reserves a mirrored, imported or orphaned name until an administrator assigns it.
    /// The column type follows the existing Identity user key.
    /// </summary>
    public required string? OwnerUserId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(OwnerUserId))]
    public User? OwnerUser { get; set; }

    [ConcurrencyCheck]
    public bool IsLocallyPublished { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [InverseProperty(nameof(Package.PackageFamily))]
    public IEnumerable<Package> Versions { get; init; } = new List<Package>();
}
