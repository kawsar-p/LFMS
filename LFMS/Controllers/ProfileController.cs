using LFMS.Data;
using LFMS.Models;
using LFMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LFMS.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IWebHostEnvironment _env;

    public ProfileController(ApplicationDbContext db, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, IWebHostEnvironment env)
    {
        _db = db;
        _users = users;
        _signIn = signIn;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        ViewBag.ProfileImagePath = user.ProfileImagePath;
        return View(new ProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? "",
            PhoneNumber = user.PhoneNumber ?? ""
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ProfileViewModel model, IFormFile? ProfileImage)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        if (!ModelState.IsValid)
        {
            ViewBag.ProfileImagePath = user.ProfileImagePath;
            return View(model);
        }

        var email = model.Email.Trim();
        var existing = await _users.FindByEmailAsync(email);
        if (existing != null && existing.Id != user.Id)
        {
            ModelState.AddModelError(nameof(model.Email), "This email is already in use.");
            return View(model);
        }

        user.FullName = model.FullName.Trim();
        user.PhoneNumber = model.PhoneNumber.Trim();

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

        if (ProfileImage != null && ProfileImage.Length > 0)
        {
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(ProfileImage.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext) || ProfileImage.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError("", "Profile image must be JPG, PNG or WEBP and 5 MB or less.");
                ViewBag.ProfileImagePath = user.ProfileImagePath;
                return View(model);
            }

            var folder = Path.Combine(_env.WebRootPath, "uploads", "profiles");
            Directory.CreateDirectory(folder);
            if (!string.IsNullOrWhiteSpace(user.ProfileImagePath))
            {
                var oldPath = Path.Combine(_env.WebRootPath, user.ProfileImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
            }
            var fileName = Guid.NewGuid().ToString("N") + ext;
            var fullPath = Path.Combine(folder, fileName);
            await using var stream = System.IO.File.Create(fullPath);
            await ProfileImage.CopyToAsync(stream);
            user.ProfileImagePath = "/uploads/profiles/" + fileName;
        }

        await _users.UpdateAsync(user);
        await _users.UpdateSecurityStampAsync(user);
        await _signIn.RefreshSignInAsync(user);

        TempData["Success"] = "Profile updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Public(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var target = await _users.FindByIdAsync(id);
        if (target == null || !target.IsActive) return NotFound();

        var current = await _users.GetUserAsync(User);
        var postCount = await _db.Posts.CountAsync(p => p.UserId == target.Id && p.IsActive);
        var collectedCount = await _db.Posts.CountAsync(p => p.UserId == target.Id && p.IsActive && p.Status == "Collected");

        return View(new PublicProfileViewModel
        {
            Id = target.Id,
            FullName = target.FullName,
            ProfileImagePath = target.ProfileImagePath,
            PhoneNumber = target.PhoneNumber,
            MemberSince = null,
            PostCount = postCount,
            CollectedCount = collectedCount,
            CanMessage = current != null && current.Id != target.Id
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        if (await _users.IsInRoleAsync(user, "Admin"))
        {
            TempData["Error"] = "The administrator account cannot be deleted here.";
            return RedirectToAction(nameof(Index));
        }

        // Remove dependent records first because LFMS intentionally uses restricted
        // user foreign keys for posts/comments/likes.
        var postIds = await _db.Posts.Where(p => p.UserId == user.Id).Select(p => p.Id).ToListAsync();

        var postImages = await _db.PostImages.Where(i => postIds.Contains(i.PostId)).ToListAsync();
        foreach (var image in postImages)
        {
            var physicalPath = Path.Combine(
                _env.WebRootPath,
                image.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (System.IO.File.Exists(physicalPath))
                System.IO.File.Delete(physicalPath);
        }

        _db.PostImages.RemoveRange(postImages);
        _db.Likes.RemoveRange(await _db.Likes.Where(x => x.UserId == user.Id).ToListAsync());
        _db.Comments.RemoveRange(await _db.Comments.Where(x => x.UserId == user.Id).ToListAsync());
        _db.Notifications.RemoveRange(await _db.Notifications.Where(x => x.UserId == user.Id).ToListAsync());
        _db.ChatMessages.RemoveRange(await _db.ChatMessages.Where(x => x.SenderId == user.Id || x.ReceiverId == user.Id).ToListAsync());

        if (!string.IsNullOrWhiteSpace(user.ProfileImagePath))
        {
            var profilePath = Path.Combine(_env.WebRootPath, user.ProfileImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(profilePath)) System.IO.File.Delete(profilePath);
        }

        var posts = await _db.Posts.Where(p => p.UserId == user.Id).ToListAsync();
        _db.Posts.RemoveRange(posts);

        await _db.SaveChangesAsync();

        var result = await _users.DeleteAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                TempData["Error"] = error.Description;
            return RedirectToAction(nameof(Index));
        }

        await _signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }
}
