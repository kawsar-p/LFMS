using LFMS.Data;
using LFMS.Models;
using LFMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LFMS.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IWebHostEnvironment _env;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> users, IWebHostEnvironment env)
    {
        _db = db;
        _users = users;
        _env = env;
    }
    public async Task<IActionResult> Dashboard()
    {
        var today = DateTime.UtcNow.Date;
        var startDate = today.AddDays(-6);

        var activePosts = _db.Posts.Where(p => p.IsActive && p.Status != "Collected");

        var adminUsers = await _users.GetUsersInRoleAsync("Admin");
        var adminIds = adminUsers.Select(u => u.Id).ToList();

        var model = new AdminDashboardViewModel
        {
            Users = await _users.Users.CountAsync(),
            Posts = await activePosts.CountAsync(),
            Comments = await _db.Comments.CountAsync(),
            Lost = await activePosts.CountAsync(p => p.PostType == "Lost"),
            Found = await activePosts.CountAsync(p => p.PostType == "Found"),
            Collected = await _db.Posts.CountAsync(p => p.IsActive && p.Status == "Collected"),
            Available = await _db.Posts.CountAsync(p => p.IsActive && p.Status == "Available"),
            UnreadNotifications = await _db.Notifications.CountAsync(n => adminIds.Contains(n.UserId) && !n.IsRead),
            CollectionRequests = await _db.CollectionConfirmations.CountAsync(x => x.Status == "PendingAdminApproval" || x.Status == "OwnerApproved")
        };

        var recentPosts = await _db.Posts
            .Where(p => p.IsActive && p.CreatedAt >= startDate)
            .Select(p => new { p.CreatedAt, p.PostType })
            .ToListAsync();

        for (var i = 0; i < 7; i++)
        {
            var day = startDate.AddDays(i);
            model.ChartLabels.Add(day.ToLocalTime().ToString("dd MMM"));
            model.ChartLost.Add(recentPosts.Count(p => p.CreatedAt.ToLocalTime().Date == day && p.PostType == "Lost"));
            model.ChartFound.Add(recentPosts.Count(p => p.CreatedAt.ToLocalTime().Date == day && p.PostType == "Found"));
        }

        return View(model);
    }

    public async Task<IActionResult> CollectionHistory()
    {
        var history = await _db.CollectionConfirmations
            .Include(x => x.Post)
                .ThenInclude(p => p!.Category)
            .Include(x => x.User)
            .OrderByDescending(x => x.ConfirmedAt)
            .ToListAsync();

        return View(history);
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveClaim(int id)
    {
        var admin = await _users.GetUserAsync(User);
        if (admin == null) return Challenge();
        var claim = await _db.CollectionConfirmations.Include(x => x.Post).FirstOrDefaultAsync(x => x.Id == id);
        if (claim == null || claim.Post == null) return NotFound();
        if (claim.Status != "PendingAdminApproval" && claim.Status != "OwnerApproved") return BadRequest();

        claim.Status = "Collected";
        claim.AdminApprovalUserId = admin.Id;
        claim.AdminApprovedAt = DateTime.UtcNow;
        claim.Post.Status = "Collected";
        _db.Notifications.Add(new Notification
        {
            UserId = claim.UserId,
            Message = $"Your claim for '{claim.Post.Title}' was approved by admin. The item is now marked collected.",
            Link = $"/Posts/Details/{claim.PostId}"
        });
        if (claim.Post.PostType == "Found" && claim.Post.UserId != claim.UserId)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = claim.Post.UserId,
                Message = $"Your found item '{claim.Post.Title}' has been approved for collection by admin.",
                Link = $"/Posts/Details/{claim.PostId}"
            });
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = "Claim approved and item marked collected.";
        return RedirectToAction(nameof(CollectionHistory));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectClaim(int id, string? notes)
    {
        var admin = await _users.GetUserAsync(User);
        if (admin == null) return Challenge();
        var claim = await _db.CollectionConfirmations.Include(x => x.Post).FirstOrDefaultAsync(x => x.Id == id);
        if (claim == null || claim.Post == null) return NotFound();
        if (claim.Status is "Collected" or "Rejected") return BadRequest();
        claim.Status = "Rejected";
        claim.ReviewNotes = string.IsNullOrWhiteSpace(notes) ? "Claim rejected by administrator." : notes.Trim();
        _db.Notifications.Add(new Notification
        {
            UserId = claim.UserId,
            Message = $"Your claim for '{claim.Post.Title}' was rejected by admin.",
            Link = $"/Posts/Details/{claim.PostId}"
        });
        await _db.SaveChangesAsync();
        TempData["Info"] = "Claim rejected.";
        return RedirectToAction(nameof(CollectionHistory));
    }

    public async Task<IActionResult> Posts() => View(await _db.Posts.Include(p => p.User).Include(p => p.Category).Where(p => p.IsActive).OrderByDescending(p => p.CreatedAt).ToListAsync());
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> DeletePost(int id) { var p = await _db.Posts.FindAsync(id); if (p != null) { p.IsActive = false; await _db.SaveChangesAsync(); } return RedirectToAction(nameof(Posts)); }
    public async Task<IActionResult> Comments() => View(await _db.Comments.Include(c => c.User).Include(c => c.Post).OrderByDescending(c => c.CreatedAt).ToListAsync());
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> DeleteComment(int id) { var c = await _db.Comments.FindAsync(id); if (c != null) { _db.Comments.Remove(c); await _db.SaveChangesAsync(); } return RedirectToAction(nameof(Comments)); }
    public async Task<IActionResult> Categories() => View(await _db.Categories.OrderBy(c => c.Name).ToListAsync());
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> AddCategory(string name) { if (!string.IsNullOrWhiteSpace(name) && !await _db.Categories.AnyAsync(c => c.Name == name.Trim())) { _db.Categories.Add(new Category { Name = name.Trim() }); await _db.SaveChangesAsync(); } return RedirectToAction(nameof(Categories)); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> DeleteCategory(int id) { var c = await _db.Categories.Include(x => x.Posts).FirstOrDefaultAsync(x => x.Id == id); if (c != null && !c.Posts.Any()) { _db.Categories.Remove(c); await _db.SaveChangesAsync(); } return RedirectToAction(nameof(Categories)); }
    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var users = await _users.Users.OrderBy(u => u.FullName).ToListAsync();
        var rows = new List<AdminUserViewModel>();

        foreach (var user in users)
        {
            var roles = await _users.GetRolesAsync(user);
            rows.Add(new AdminUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? "",
                PhoneNumber = user.PhoneNumber ?? "",
                IsActive = user.IsActive,
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                Role = roles.FirstOrDefault() ?? "User",
                ProfileImagePath = user.ProfileImagePath
            });
        }

        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> EditUser(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();

        var roles = await _users.GetRolesAsync(user);
        return View(new AdminUserViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? "",
            PhoneNumber = user.PhoneNumber ?? "",
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            Role = roles.FirstOrDefault() ?? "User",
                ProfileImagePath = user.ProfileImagePath
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(AdminUserViewModel model)
    {
        var user = await _users.FindByIdAsync(model.Id);
        if (user == null) return NotFound();

        var currentAdmin = await _users.GetUserAsync(User);
        if (currentAdmin == null) return Challenge();

        // An admin cannot remove the Admin role from the last/current admin accidentally.
        if (user.Id == currentAdmin.Id)
        {
            if (model.Role != "Admin")
                ModelState.AddModelError(nameof(model.Role), "You cannot remove the Admin role from your own account.");

            if (!model.IsActive)
                ModelState.AddModelError(nameof(model.IsActive), "You cannot deactivate your own account.");
        }

        var existingEmail = await _users.FindByEmailAsync(model.Email.Trim());
        if (existingEmail != null && existingEmail.Id != user.Id)
            ModelState.AddModelError(nameof(model.Email), "This email is already in use.");

        if (!ModelState.IsValid)
            return View(model);

        user.FullName = model.FullName.Trim();
        user.PhoneNumber = model.PhoneNumber?.Trim() ?? "";
        user.IsActive = model.IsActive;
        user.EmailConfirmed = model.EmailConfirmed;
        user.PhoneNumberConfirmed = model.PhoneNumberConfirmed;

        var email = model.Email.Trim();
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await _users.SetEmailAsync(user, email);
            if (!emailResult.Succeeded)
            {
                foreach (var error in emailResult.Errors)
                    ModelState.AddModelError(nameof(model.Email), error.Description);
                return View(model);
            }

            var usernameResult = await _users.SetUserNameAsync(user, email);
            if (!usernameResult.Succeeded)
            {
                foreach (var error in usernameResult.Errors)
                    ModelState.AddModelError(nameof(model.Email), error.Description);
                return View(model);
            }
        }

        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await _users.ResetPasswordAsync(user, token, model.NewPassword);
            if (!passwordResult.Succeeded)
            {
                foreach (var error in passwordResult.Errors)
                    ModelState.AddModelError(nameof(model.NewPassword), error.Description);
                return View(model);
            }
        }

        var currentRoles = await _users.GetRolesAsync(user);
        var requestedRole = model.Role == "Admin" ? "Admin" : "User";

        if (!currentRoles.Contains(requestedRole))
        {
            if (currentRoles.Count > 0)
                await _users.RemoveFromRolesAsync(user, currentRoles);

            await _users.AddToRoleAsync(user, requestedRole);
        }

        if (model.ProfileImage != null && model.ProfileImage.Length > 0)
        {
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(model.ProfileImage.FileName).ToLowerInvariant();

            if (!allowed.Contains(ext) || model.ProfileImage.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError(nameof(model.ProfileImage), "Profile image must be JPG, PNG or WEBP and 5 MB or less.");
                model.ProfileImagePath = user.ProfileImagePath;
                return View(model);
            }

            var folder = Path.Combine(_env.WebRootPath, "uploads", "profiles");
            Directory.CreateDirectory(folder);

            if (!string.IsNullOrWhiteSpace(user.ProfileImagePath))
            {
                var oldPath = Path.Combine(
                    _env.WebRootPath,
                    user.ProfileImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);
            }

            var fileName = Guid.NewGuid().ToString("N") + ext;
            var fullPath = Path.Combine(folder, fileName);

            await using var stream = System.IO.File.Create(fullPath);
            await model.ProfileImage.CopyToAsync(stream);

            user.ProfileImagePath = "/uploads/profiles/" + fileName;
        }

        var updateResult = await _users.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        // If an admin deactivates another currently logged-in account, invalidate its security stamp.
        await _users.UpdateSecurityStampAsync(user);

        TempData["Success"] = $"User '{user.FullName}' updated successfully.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUserStatus(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();

        var currentAdmin = await _users.GetUserAsync(User);
        if (currentAdmin != null && user.Id == currentAdmin.Id)
        {
            TempData["Error"] = "You cannot deactivate your own admin account.";
            return RedirectToAction(nameof(Users));
        }

        user.IsActive = !user.IsActive;
        await _users.UpdateAsync(user);
        await _users.UpdateSecurityStampAsync(user);

        TempData["Success"] = $"{user.FullName} is now {(user.IsActive ? "Active" : "Inactive")}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();

        var currentAdmin = await _users.GetUserAsync(User);
        if (currentAdmin != null && user.Id == currentAdmin.Id)
        {
            TempData["Error"] = "You cannot delete your own admin account.";
            return RedirectToAction(nameof(Users));
        }

        var postIds = await _db.Posts.Where(p => p.UserId == user.Id).Select(p => p.Id).ToListAsync();
        var images = await _db.PostImages.Where(i => postIds.Contains(i.PostId)).ToListAsync();

        foreach (var image in images)
        {
            var physicalPath = Path.Combine(
                HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath,
                image.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (System.IO.File.Exists(physicalPath))
                System.IO.File.Delete(physicalPath);
        }

        _db.PostImages.RemoveRange(images);
        _db.Likes.RemoveRange(await _db.Likes.Where(x => x.UserId == user.Id).ToListAsync());
        _db.Comments.RemoveRange(await _db.Comments.Where(x => x.UserId == user.Id).ToListAsync());
        _db.Notifications.RemoveRange(await _db.Notifications.Where(x => x.UserId == user.Id).ToListAsync());
        _db.ChatMessages.RemoveRange(await _db.ChatMessages.Where(x => x.SenderId == user.Id || x.ReceiverId == user.Id).ToListAsync());
        _db.CollectionConfirmations.RemoveRange(await _db.CollectionConfirmations.Where(x => x.UserId == user.Id || postIds.Contains(x.PostId)).ToListAsync());

        if (!string.IsNullOrWhiteSpace(user.ProfileImagePath))
        {
            var profilePath = Path.Combine(
                HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath,
                user.ProfileImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(profilePath)) System.IO.File.Delete(profilePath);
        }

        _db.Posts.RemoveRange(await _db.Posts.Where(x => x.UserId == user.Id).ToListAsync());

        await _db.SaveChangesAsync();

        var result = await _users.DeleteAsync(user);
        TempData[result.Succeeded ? "Success" : "Error"] =
            result.Succeeded ? "User deleted successfully." : "Could not delete the user.";

        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var admin = await _users.GetUserAsync(User);
        if (admin == null) return Challenge();

        return View(new AdminSettingsViewModel
        {
            Email = admin.Email ?? admin.UserName ?? ""
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(AdminSettingsViewModel model)
    {
        var admin = await _users.GetUserAsync(User);
        if (admin == null) return Challenge();

        if (!ModelState.IsValid) return View(model);

        var email = model.Email.Trim();
        var existing = await _users.FindByEmailAsync(email);
        if (existing != null && existing.Id != admin.Id)
        {
            ModelState.AddModelError(nameof(model.Email), "This email is already in use.");
            return View(model);
        }

        if (!string.Equals(admin.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await _users.SetEmailAsync(admin, email);
            if (!emailResult.Succeeded)
            {
                foreach (var error in emailResult.Errors)
                    ModelState.AddModelError(nameof(model.Email), error.Description);
                return View(model);
            }

            var usernameResult = await _users.SetUserNameAsync(admin, email);
            if (!usernameResult.Succeeded)
            {
                foreach (var error in usernameResult.Errors)
                    ModelState.AddModelError(nameof(model.Email), error.Description);
                return View(model);
            }
        }

        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            var token = await _users.GeneratePasswordResetTokenAsync(admin);
            var passwordResult = await _users.ResetPasswordAsync(admin, token, model.NewPassword);
            if (!passwordResult.Succeeded)
            {
                foreach (var error in passwordResult.Errors)
                    ModelState.AddModelError(nameof(model.NewPassword), error.Description);
                return View(model);
            }
        }

        await _users.UpdateAsync(admin);
        await _users.UpdateSecurityStampAsync(admin);
        await HttpContext.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>().RefreshSignInAsync(admin);

        TempData["Success"] = "Admin account settings updated successfully.";
        return RedirectToAction(nameof(Settings));
    }
}
