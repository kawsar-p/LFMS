using LFMS.Data;
using LFMS.Models;
using LFMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LFMS.Controllers;

[Authorize]
public class PostsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;
    public PostsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IWebHostEnvironment env) { _db = db; _userManager = userManager; _env = env; }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var post = await _db.Posts.Include(p => p.User).Include(p => p.Category).Include(p => p.Comments).ThenInclude(c => c.User).Include(p => p.Likes).Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
        if (post == null) return NotFound();
        ViewBag.CollectionClaims = await _db.CollectionConfirmations.Include(x => x.User).Where(x => x.PostId == id && x.Status != "Rejected").OrderByDescending(x => x.ConfirmedAt).ToListAsync();
        return View(post);
    }

    public async Task<IActionResult> Create() { ViewBag.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync(); return View(new PostCreateViewModel()); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PostCreateViewModel model)
    {
        if (model.Images == null || model.Images.Count == 0)
            ModelState.AddModelError("Images", "At least one image is required.");

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (model.Images != null)
        {
            if (model.Images.Count > 8)
                ModelState.AddModelError("Images", "You can upload up to 8 images.");

            foreach (var image in model.Images ?? new List<IFormFile>())
            {
                var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
                if (!allowed.Contains(ext) || image.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("Images", $"{image.FileName}: use JPG, PNG or WEBP up to 5 MB per image.");
                }
            }
        }

        if (!ModelState.IsValid) { ViewBag.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync(); return View(model); }

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var folder = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(folder);
        var savedImages = new List<string>();
        foreach (var image in (model.Images ?? new List<IFormFile>()))
        {
            var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
            var name = Guid.NewGuid() + ext;
            await using var stream = System.IO.File.Create(Path.Combine(folder, name));
            await image.CopyToAsync(stream);
            savedImages.Add("/uploads/" + name);
        }
        var post = new Post
        {
            Title = model.Title.Trim(),
            Description = model.Description.Trim(),
            PrivateVerificationDetails = string.IsNullOrWhiteSpace(model.PrivateVerificationDetails) ? null : model.PrivateVerificationDetails.Trim(),
            PostType = model.PostType,
            Location = model.Location.Trim(),
            LostFoundDate = model.LostFoundDate,
            CategoryId = model.CategoryId,
            ImagePath = savedImages.First(),
            UserId = user.Id,
            ReferenceCode = "TEMP",
            Status = "Available"
        };
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        foreach (var path in savedImages)
            _db.PostImages.Add(new PostImage { PostId = post.Id, ImagePath = path });
        await _db.SaveChangesAsync();
        post.ReferenceCode = $"LF-{post.Id:D6}";
        await _db.SaveChangesAsync();

        // Notify every admin that a new post was created.
        var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
        foreach (var admin in adminUsers.Where(a => a.Id != user.Id))
        {
            _db.Notifications.Add(new Notification
            {
                UserId = admin.Id,
                Message = $"{user.FullName} created a new {post.PostType.ToLowerInvariant()} post: {post.Title}.",
                Link = $"/Posts/Details/{post.Id}"
            });
        }
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = post.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var post = await _db.Posts.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
        if (post == null) return NotFound();
        if (post.UserId != user.Id && !User.IsInRole("Admin")) return Forbid();
        ViewBag.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
        ViewBag.ExistingImageEntities = post.Images.ToList();
        return View(new PostCreateViewModel { Id = post.Id, Title = post.Title, Description = post.Description, PrivateVerificationDetails = post.PrivateVerificationDetails ?? "", PostType = post.PostType, Location = post.Location, LostFoundDate = post.LostFoundDate, CategoryId = post.CategoryId, ExistingImages = post.Images.Select(x => x.ImagePath).ToList() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("Edit")]
    public async Task<IActionResult> EditSave(int id)
    {
        // Read the form directly so optional file fields can never make the
        // normal Edit save fail during model binding.
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var post = await _db.Posts
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (post == null) return NotFound();
        if (post.UserId != user.Id && !User.IsInRole("Admin")) return Forbid();

        var form = await Request.ReadFormAsync();

        var title = (form["Title"].FirstOrDefault() ?? "").Trim();
        var description = (form["Description"].FirstOrDefault() ?? "").Trim();
        var postType = (form["PostType"].FirstOrDefault() ?? "").Trim();
        var location = (form["Location"].FirstOrDefault() ?? "").Trim();
        var privateVerification = (form["PrivateVerificationDetails"].FirstOrDefault() ?? "").Trim();
        var categoryText = form["CategoryId"].FirstOrDefault();
        var dateText = form["LostFoundDate"].FirstOrDefault();

        var errors = new List<string>();

        // Edit fields are intentionally optional. If a field is left blank,
        // keep the current saved value instead of blocking the update.
        var finalTitle = string.IsNullOrWhiteSpace(title) ? post.Title : title;
        var finalDescription = string.IsNullOrWhiteSpace(description) ? post.Description : description;
        var finalPostType = postType is "Lost" or "Found" ? postType : post.PostType;
        var finalLocation = string.IsNullOrWhiteSpace(location) ? post.Location : location;
        var finalPrivateVerification = privateVerification;

        if (finalPrivateVerification.Length > 2000)
            errors.Add("Private verification details are too long.");
        if (finalTitle.Length > 120)
            errors.Add("Item title is too long.");
        if (finalDescription.Length > 2000)
            errors.Add("Description is too long.");
        if (finalLocation.Length > 150)
            errors.Add("Location is too long.");

        var categoryId = post.CategoryId;
        if (!string.IsNullOrWhiteSpace(categoryText))
        {
            if (!int.TryParse(categoryText, out var requestedCategoryId) || requestedCategoryId <= 0)
                errors.Add("The selected category is invalid.");
            else
                categoryId = requestedCategoryId;
        }

        var lostFoundDate = post.LostFoundDate;
        if (!string.IsNullOrWhiteSpace(dateText))
        {
            if (!DateTime.TryParseExact(
                    dateText,
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var requestedDate))
                errors.Add("The selected date is invalid.");
            else
                lostFoundDate = requestedDate;
        }

        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == categoryId);
        if (!categoryExists)
            errors.Add("The selected category does not exist.");

        var newImages = form.Files
            .Where(f => string.Equals(f.Name, "Images", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };

        if (newImages.Count > 8)
            errors.Add("You can upload up to 8 new images.");

        foreach (var image in newImages)
        {
            var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext))
                errors.Add($"{image.FileName}: use JPG, PNG or WEBP.");
            else if (image.Length > 5 * 1024 * 1024)
                errors.Add($"{image.FileName}: image size must be 5 MB or less.");
        }

        if (errors.Count > 0)
        {
            ViewBag.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.ExistingImageEntities = post.Images.ToList();

            var vm = new PostCreateViewModel
            {
                Id = post.Id,
                Title = finalTitle,
                Description = finalDescription,
                PrivateVerificationDetails = privateVerification,
                PostType = finalPostType,
                Location = finalLocation,
                LostFoundDate = lostFoundDate,
                CategoryId = categoryId,
                ExistingImages = post.Images.Select(x => x.ImagePath).ToList()
            };

            foreach (var error in errors)
                ModelState.AddModelError("", error);

            return View(vm);
        }

        // Update only after every value has been validated.
        post.Title = finalTitle;
        post.Description = finalDescription;
        post.PrivateVerificationDetails = string.IsNullOrWhiteSpace(finalPrivateVerification) ? null : finalPrivateVerification;
        post.PostType = finalPostType;
        post.Location = finalLocation;
        post.LostFoundDate = lostFoundDate;
        post.CategoryId = categoryId;

        if (newImages.Count > 0)
        {
            var folder = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(folder);

            foreach (var image in newImages)
            {
                var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
                var name = Guid.NewGuid().ToString("N") + ext;
                var fullPath = Path.Combine(folder, name);

                await using var stream = System.IO.File.Create(fullPath);
                await image.CopyToAsync(stream);

                var relativePath = "/uploads/" + name;

                _db.PostImages.Add(new PostImage
                {
                    PostId = post.Id,
                    ImagePath = relativePath
                });

                if (string.IsNullOrWhiteSpace(post.ImagePath))
                    post.ImagePath = relativePath;
            }
        }

        await _db.SaveChangesAsync();

        // Keep the legacy image field correct.
        if (string.IsNullOrWhiteSpace(post.ImagePath))
        {
            post.ImagePath = await _db.PostImages
                .Where(x => x.PostId == post.Id)
                .OrderBy(x => x.Id)
                .Select(x => x.ImagePath)
                .FirstOrDefaultAsync() ?? "";

            await _db.SaveChangesAsync();
        }

        TempData["Success"] = "Post updated successfully.";
        return RedirectToAction(nameof(Details), new { id = post.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveImage(int imageId, int postId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var post = await _db.Posts.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == postId);
        if (post == null) return NotFound();
        if (post.UserId != user.Id && !User.IsInRole("Admin")) return Forbid();

        var image = post.Images.FirstOrDefault(x => x.Id == imageId);
        if (image == null) return NotFound();

        var physicalPath = Path.Combine(_env.WebRootPath, image.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(physicalPath))
            System.IO.File.Delete(physicalPath);

        _db.PostImages.Remove(image);

        var remaining = post.Images.Where(x => x.Id != imageId).Select(x => x.ImagePath).FirstOrDefault();
        post.ImagePath = remaining;

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = postId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var post = await _db.Posts.FindAsync(id);
        if (post == null) return NotFound();
        if (post.UserId != user.Id && !User.IsInRole("Admin")) return Forbid();
        post.IsActive = false; await _db.SaveChangesAsync(); return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Like(int postId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var post = await _db.Posts.FindAsync(postId);
        if (post == null || !post.IsActive) return NotFound();

        var existing = await _db.Likes.FirstOrDefaultAsync(x => x.PostId == postId && x.UserId == user.Id);
        if (existing == null)
            _db.Likes.Add(new Like { PostId = postId, UserId = user.Id });
        else
            _db.Likes.Remove(existing);

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = postId });
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> CollectionConfirmation(int id)
    {
        var post = await _db.Posts.Include(p => p.Category).Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
        if (post == null) return NotFound();
        if (post.Status == "Collected")
        {
            TempData["Info"] = "This item has already been collected.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var user = await _userManager.GetUserAsync(User);
        if (post.PostType == "Lost" && user is not null && post.UserId != user.Id && !User.IsInRole("Admin"))
            return Forbid();

        if (user is not null)
        {
            var pending = await _db.CollectionConfirmations.AnyAsync(x => x.PostId == id && x.UserId == user.Id && x.Status != "Rejected");
            if (pending)
            {
                TempData["Info"] = "You already have a claim/recovery request for this item.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        return View(new CollectionConfirmationViewModel
        {
            PostId = post.Id,
            PostType = post.PostType,
            Title = post.Title,
            FullName = user?.FullName ?? "",
            PhoneNumber = user?.PhoneNumber ?? "",
            HandoverDate = DateTime.Today
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CollectionConfirmation(CollectionConfirmationViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            var returnUrl = Url.Action(nameof(CollectionConfirmation), "Posts", new { id = model.PostId });
            return RedirectToAction("Login", "Account", new { returnUrl });
        }

        var post = await _db.Posts.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == model.PostId && p.IsActive);
        if (post == null) return NotFound();
        if (post.Status == "Collected") return RedirectToAction(nameof(Details), new { id = post.Id });
        if (post.PostType == "Lost" && post.UserId != user.Id && !User.IsInRole("Admin")) return Forbid();

        if (model.HandoverDate > DateTime.Today)
            ModelState.AddModelError(nameof(model.HandoverDate), "The confirmation date cannot be in the future.");
        if (!model.Confirmed)
            ModelState.AddModelError(nameof(model.Confirmed), "You must confirm that the information is true.");

        if (!ModelState.IsValid)
        {
            model.PostType = post.PostType;
            model.Title = post.Title;
            return View(model);
        }

        var duplicate = await _db.CollectionConfirmations.AnyAsync(x => x.PostId == post.Id && x.UserId == user.Id && x.Status != "Rejected");
        if (duplicate)
        {
            TempData["Info"] = "You already have a pending request for this item.";
            return RedirectToAction(nameof(Details), new { id = post.Id });
        }

        var status = post.PostType == "Found" ? "PendingOwnerApproval" : "PendingAdminApproval";
        var confirmationType = post.PostType == "Lost" ? "LostItemReceived" : "FoundItemClaim";
        var confirmation = new CollectionConfirmation
        {
            PostId = post.Id,
            UserId = user.Id,
            FullName = user.FullName,
            PhoneNumber = model.PhoneNumber?.Trim() ?? "",
            ConfirmationType = confirmationType,
            IdentificationDetails = model.IdentificationDetails.Trim(),
            ClaimantVerificationAnswer = model.ClaimantVerificationAnswer.Trim(),
            VerificationReferenceAtSubmission = post.PrivateVerificationDetails,
            HandoverDetails = model.HandoverDetails.Trim(),
            HandoverDate = model.HandoverDate.Date,
            Status = status,
            ConfirmedAt = DateTime.UtcNow
        };
        _db.CollectionConfirmations.Add(confirmation);

        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        var adminMessage = post.PostType == "Found"
            ? $"{user.FullName} submitted a claim for '{post.Title}'. The finder must approve it before admin review."
            : $"{user.FullName} submitted a lost-item recovery confirmation for '{post.Title}'. Admin review is required.";
        foreach (var admin in admins.Where(a => a.Id != user.Id))
            _db.Notifications.Add(new Notification { UserId = admin.Id, Message = adminMessage, Link = "/Admin/CollectionHistory" });

        if (post.PostType == "Found" && post.UserId != user.Id)
            _db.Notifications.Add(new Notification { UserId = post.UserId, Message = $"{user.FullName} submitted a claim for your found item '{post.Title}'. Please review the claim.", Link = $"/Posts/Details/{post.Id}" });

        await _db.SaveChangesAsync();
        TempData["Success"] = post.PostType == "Found"
            ? "Claim submitted. The finder must approve it before admin review."
            : "Recovery confirmation submitted. Admin review is required before the item is marked collected.";
        return RedirectToAction(nameof(Details), new { id = post.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveClaimByOwner(int id)
    {
        var owner = await _userManager.GetUserAsync(User);
        if (owner == null) return Challenge();
        var claim = await _db.CollectionConfirmations.Include(x => x.Post).FirstOrDefaultAsync(x => x.Id == id);
        if (claim?.Post == null) return NotFound();
        if (claim.Post.PostType != "Found" || claim.Post.UserId != owner.Id) return Forbid();
        if (claim.Status != "PendingOwnerApproval") return BadRequest();

        claim.Status = "OwnerApproved";
        claim.OwnerApprovalUserId = owner.Id;
        claim.OwnerApprovedAt = DateTime.UtcNow;

        var otherClaims = await _db.CollectionConfirmations
            .Where(x => x.PostId == claim.PostId && x.Id != claim.Id && x.Status == "PendingOwnerApproval")
            .ToListAsync();
        foreach (var other in otherClaims)
        {
            other.Status = "Rejected";
            other.ReviewNotes = "Another claimant was approved by the finder.";
            _db.Notifications.Add(new Notification
            {
                UserId = other.UserId,
                Message = $"Your claim for '{claim.Post.Title}' was not selected by the finder.",
                Link = $"/Posts/Details/{claim.PostId}"
            });
        }

        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        foreach (var admin in admins)
            _db.Notifications.Add(new Notification
            {
                UserId = admin.Id,
                Message = $"The finder approved a claim for '{claim.Post.Title}'. Admin review is now required.",
                Link = "/Admin/CollectionHistory"
            });
        _db.Notifications.Add(new Notification
        {
            UserId = claim.UserId,
            Message = $"The finder approved your claim for '{claim.Post.Title}'. It is now waiting for admin review.",
            Link = "/Posts/Details/" + claim.PostId
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Claim approved by the finder. Admin review is now required.";
        return RedirectToAction(nameof(Details), new { id = claim.PostId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectClaimByOwner(int id, string? notes)
    {
        var owner = await _userManager.GetUserAsync(User);
        if (owner == null) return Challenge();
        var claim = await _db.CollectionConfirmations.Include(x => x.Post).FirstOrDefaultAsync(x => x.Id == id);
        if (claim?.Post == null) return NotFound();
        if (claim.Post.PostType != "Found" || claim.Post.UserId != owner.Id) return Forbid();
        if (claim.Status != "PendingOwnerApproval") return BadRequest();
        claim.Status = "Rejected";
        claim.ReviewNotes = string.IsNullOrWhiteSpace(notes) ? "Claim rejected by the finder/post owner." : notes.Trim();
        _db.Notifications.Add(new Notification
        {
            UserId = claim.UserId,
            Message = $"Your claim for '{claim.Post.Title}' was rejected by the finder.",
            Link = "/Posts/Details/" + claim.PostId
        });
        await _db.SaveChangesAsync();
        TempData["Info"] = "Claim rejected by the finder.";
        return RedirectToAction(nameof(Details), new { id = claim.PostId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, string status)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var post = await _db.Posts.FindAsync(id);
        if (post == null || !post.IsActive) return NotFound();

        if (post.UserId != user.Id && !User.IsInRole("Admin"))
            return Forbid();

        status = status?.Trim() ?? "";
        if (status != "Available" && status != "Collected")
            return BadRequest();

        if (status == "Collected" && !User.IsInRole("Admin"))
            return BadRequest("Items can only be marked collected after the claim/recovery approval workflow.");

        post.Status = status;
        await _db.SaveChangesAsync();

        if (status == "Collected")
        {
            if (post.UserId != user.Id)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = post.UserId,
                    Message = $"An administrator marked your post '{post.Title}' as collected.",
                    Link = $"/Posts/Details/{post.Id}"
                });
            }
        }

        if (post.UserId == user.Id)
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            foreach (var admin in admins)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = admin.Id,
                    Message = $"{user.FullName} marked post '{post.Title}' as {status.ToLowerInvariant()}.",
                    Link = $"/Posts/Details/{post.Id}"
                });
            }
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = post.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Comment(int postId, string content)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > 500)
            return RedirectToAction(nameof(Details), new { id = postId });

        var post = await _db.Posts.FindAsync(postId);
        if (post == null || !post.IsActive) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        _db.Comments.Add(new Comment
        {
            PostId = postId,
            UserId = user.Id,
            Content = content.Trim()
        });

        await _db.SaveChangesAsync();

        // Notify the post owner about a new comment.
        if (post.UserId != user.Id)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = post.UserId,
                Message = $"{user.FullName} commented on your post '{post.Title}'.",
                Link = $"/Posts/Details/{postId}"
            });
        }

        // Admins also receive a notification so they can monitor community activity.
        if (!User.IsInRole("Admin"))
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            foreach (var admin in admins)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = admin.Id,
                    Message = $"{user.FullName} commented on '{post.Title}'.",
                    Link = $"/Posts/Details/{postId}"
                });
            }
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = postId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var comment = await _db.Comments.FindAsync(id);
        if (comment == null) return NotFound();
        if (comment.UserId != user.Id && !User.IsInRole("Admin")) return Forbid();

        var postId = comment.PostId;
        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = postId });
    }

    public async Task<IActionResult> MyPosts()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var posts = await _db.Posts
            .Where(p => p.UserId == user.Id && p.IsActive)
            .Include(p => p.Category)
            .Include(p => p.Comments)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View(posts);
    }
}
