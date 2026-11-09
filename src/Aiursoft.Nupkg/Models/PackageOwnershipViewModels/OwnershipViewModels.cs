using Aiursoft.UiStack.Layout;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aiursoft.Nupkg.Models.PackageOwnershipViewModels;

public class OwnershipItem
{
    public int Id { get; set; }
    public required string PackageId { get; set; }
    public string? OwnerName { get; set; }
    public int VersionCount { get; set; }
}

public class IndexViewModel : UiStackLayoutViewModel
{
    public string Query { get; set; } = string.Empty;
    public bool UnassignedOnly { get; set; }
    public int Page { get; set; } = 1;
    public int Total { get; set; }
    public List<OwnershipItem> Items { get; set; } = [];
}

public class AssignViewModel : UiStackLayoutViewModel
{
    public int Id { get; set; }
    public string PackageId { get; set; } = string.Empty;
    public string? OwnerUserId { get; set; }
    public string? PreviousOwnerUserId { get; set; }
    public List<SelectListItem> Users { get; set; } = [];
    public string? Query { get; set; }
    public bool UnassignedOnly { get; set; }
    public int Page { get; set; } = 1;
}
