using LFMS.Data;
using LFMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LFMS.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;

    public HomeController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? search, string? type, int? categoryId)
    {
        var query = _db.Posts
            .AsNoTracking()
            .Where(p => p.IsActive && p.Status != "Collected")
            .Include(p => p.User)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Likes)
            .Include(p => p.Comments)
                .ThenInclude(c => c.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p =>
                p.ReferenceCode.Contains(search) ||
                p.Title.Contains(search) ||
                p.Description.Contains(search) ||
                p.Location.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(p => p.PostType == type);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        var vm = new HomeViewModel
        {
            Posts = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(),
            Categories = await _db.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync(),
            Search = search,
            Type = type,
            CategoryId = categoryId
        };

        return View(vm);
    }
}
