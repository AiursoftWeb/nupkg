using Aiursoft.Nupkg.Authorization;
using Aiursoft.Nupkg.Entities;
using Aiursoft.Nupkg.Models.PackageOwnershipViewModels;
using Aiursoft.Nupkg.Services;
using Aiursoft.UiStack.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Nupkg.Controllers;

[Authorize(Policy = AppPermissionNames.CanManagePackages)]
public class PackageOwnershipController(NupkgDbContext db) : Controller
{
    [RenderInNavBar(NavGroupName = "Features", NavGroupOrder = 1,
        CascadedLinksGroupName = "Packages", CascadedLinksIcon = "package", CascadedLinksOrder = 1,
        LinkText = "Package ownership", LinkOrder = 3)]
    public async Task<IActionResult> Index(string q = "", bool unassignedOnly = false, int page = 1)
    {
        page = Math.Clamp(page, 1, int.MaxValue / 20);
        var query = db.PackageFamilies.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var normalized = q.ToLowerInvariant();
            query = query.Where(r => r.NormalizedId.Contains(normalized));
        }
        if (unassignedOnly) query = query.Where(r => r.OwnerUserId == null);
        return this.StackView(new IndexViewModel
        {
            PageTitle = "Package ownership", Query = q, UnassignedOnly = unassignedOnly, Page = page,
            Total = await query.CountAsync(),
            Items = await query.OrderBy(r => r.NormalizedId).Skip((page - 1) * 20).Take(20)
                .Select(r => new OwnershipItem
                {
                    Id = r.Id, PackageId = r.PackageId,
                    OwnerName = r.OwnerUser == null ? null : r.OwnerUser.DisplayName,
                    VersionCount = r.Versions.Count
                }).ToListAsync()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Assign(int id, string q = "", bool unassignedOnly = false, int page = 1)
    {
        var registration = await db.PackageFamilies.FindAsync(id);
        if (registration == null) return NotFound();
        var model = new AssignViewModel
        {
            PageTitle = "Assign package owner", Id = id, PackageId = registration.PackageId,
            OwnerUserId = registration.OwnerUserId, PreviousOwnerUserId = registration.OwnerUserId,
            Query = q, UnassignedOnly = unassignedOnly, Page = Math.Max(1, page)
        };
        await PopulateUsersAsync(model);
        return this.StackView(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, AssignViewModel model)
    {
        var registration = await db.PackageFamilies.FindAsync(id);
        if (registration == null) return NotFound();
        if (registration.OwnerUserId != model.PreviousOwnerUserId)
        {
            ModelState.AddModelError(string.Empty, "The owner has changed. Review the current owner and try again.");
            model.OwnerUserId = registration.OwnerUserId;
            ModelState.Remove(nameof(model.OwnerUserId));
            Response.StatusCode = 409;
        }
        if (!string.IsNullOrEmpty(model.OwnerUserId) && !await db.Users.AnyAsync(u => u.Id == model.OwnerUserId))
            ModelState.AddModelError(nameof(model.OwnerUserId), "Choose an existing user.");
        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.PackageId = registration.PackageId;
            model.PageTitle = "Assign package owner";
            model.PreviousOwnerUserId = registration.OwnerUserId;
            ModelState.Remove(nameof(model.PreviousOwnerUserId));
            await PopulateUsersAsync(model);
            return this.StackView(model);
        }
        registration.OwnerUserId = string.IsNullOrEmpty(model.OwnerUserId) ? null : model.OwnerUserId;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { q = model.Query, model.UnassignedOnly, model.Page });
    }

    private async Task PopulateUsersAsync(AssignViewModel model) => model.Users = await db.Users
        .AsNoTracking().OrderBy(u => u.DisplayName)
        .Select(u => new SelectListItem { Value = u.Id, Text = u.DisplayName == u.UserName ? u.DisplayName : u.DisplayName + " (" + u.UserName + ")" }).ToListAsync();
}
