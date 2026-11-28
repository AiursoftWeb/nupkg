using System.Security.Cryptography;
using Aiursoft.Nupkg.Core;
using Aiursoft.Nupkg.Entities;
using Aiursoft.Nupkg.Models.UserApiKeysViewModels;
using Aiursoft.Nupkg.Services.Authentication;
using Aiursoft.Nupkg.Services;
using Aiursoft.UiStack.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using EFCoreSecondLevelCacheInterceptor;

namespace Aiursoft.Nupkg.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class UserApiKeysController(
    NupkgDbContext db,
    UserManager<User> users,
    IUrlGenerator urls,
    PackagePublisherAuthenticationService publishers,
    IStringLocalizer<UserApiKeysController> localizer) : Controller
{
    [RenderInNavBar(NavGroupName = "Settings", NavGroupOrder = 9998,
        CascadedLinksGroupName = "Personal", CascadedLinksIcon = "user-circle", CascadedLinksOrder = 10,
        LinkText = "API Keys", LinkOrder = 10)]
    public async Task<IActionResult> Index() => this.StackView(new IndexViewModel
    {
        PageTitle = "API Keys",
        Keys = await db.UserApiKeys.AsNoTracking().NotCacheable().Where(k => k.UserId == users.GetUserId(User))
            .OrderByDescending(k => k.CreatedAt).ToListAsync()
    });

    [HttpGet]
    public IActionResult Create() => this.StackView(new CreateViewModel { PageTitle = "Create API Key" });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.PageTitle = "Create API Key";
            return this.StackView(model);
        }

        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var key = new UserApiKey
        {
            UserId = users.GetUserId(User)!,
            Name = model.Name.Trim(),
            KeyHash = PackagePublisherAuthenticationService.ComputeSha256Hex(raw),
            KeyPrefix = raw[..8],
            ExpiresAt = DateTime.UtcNow.AddDays(model.ExpirationDays)
        };
        db.UserApiKeys.Add(key);
        await db.SaveChangesAsync();
        TempData[$"NewApiKey:{key.Id}"] = raw;
        return RedirectToAction(nameof(Usage), new { id = key.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Usage(int id)
    {
        var key = await FindKeyAsync(id);
        if (key == null) return NotFound();
        var user = await users.GetUserAsync(User);
        if (user == null) return Challenge();
        var publisher = await publishers.ForUserAsync(user);
        return this.StackView(new UsageViewModel
        {
            PageTitle = "API Key Usage",
            Key = key,
            RawKey = TempData[$"NewApiKey:{id}"] as string,
            ServiceIndexUrl = urls.GetServiceIndexUrl(),
            CanPublish = publisher.CanPublish || publisher.CanManagePackages
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var key = await FindKeyAsync(id);
        return key == null ? NotFound() : this.StackView(new EditViewModel
        {
            PageTitle = "Edit API Key",
            Id = key.Id,
            KeyPrefix = key.KeyPrefix,
            CreatedAt = key.CreatedAt,
            LastUsedAt = key.LastUsedAt,
            TotalUses = key.TotalUses,
            Name = key.Name,
            ExpiresAt = key.ExpiresAt ?? DateTime.UtcNow.AddDays(CreateViewModel.MaxExpirationDays)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditViewModel model)
    {
        var key = await FindKeyAsync(id);
        if (key == null) return NotFound();

        DateTime? utcExpiresAt = null;
        if (model.ExpiresAt.HasValue)
        {
            utcExpiresAt = model.ExpiresAt.Value.Kind switch
            {
                DateTimeKind.Utc => model.ExpiresAt.Value,
                DateTimeKind.Local => model.ExpiresAt.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(model.ExpiresAt.Value, DateTimeKind.Utc)
            };

            if (utcExpiresAt.Value > DateTime.UtcNow.AddDays(CreateViewModel.MaxExpirationDays).AddMinutes(5))
            {
                ModelState.AddModelError(nameof(model.ExpiresAt),
                    localizer["Expiration cannot be more than 100 years from now."]);
            }
        }

        if (!ModelState.IsValid)
        {
            model.PageTitle = "Edit API Key";
            model.Id = key.Id;
            model.KeyPrefix = key.KeyPrefix;
            model.CreatedAt = key.CreatedAt;
            model.LastUsedAt = key.LastUsedAt;
            model.TotalUses = key.TotalUses;
            return this.StackView(model);
        }

        key.Name = model.Name.Trim();
        key.ExpiresAt = utcExpiresAt;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return NotFound(); }
        TempData["SuccessMessage"] = "API key updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var key = await FindKeyAsync(id);
        return key == null ? NotFound() : this.StackView(new DeleteViewModel
        {
            PageTitle = "Revoke API Key",
            Key = key
        });
    }

    [HttpPost, ActionName(nameof(Delete)), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var key = await FindKeyAsync(id);
        if (key == null) return NotFound();
        db.UserApiKeys.Remove(key);
        await db.SaveChangesAsync();
        TempData["SuccessMessage"] = "API key revoked.";
        return RedirectToAction(nameof(Index));
    }

    private Task<UserApiKey?> FindKeyAsync(int id) =>
        db.UserApiKeys.NotCacheable().SingleOrDefaultAsync(k => k.Id == id && k.UserId == users.GetUserId(User));
}
