using LFMS.Data;
using LFMS.Models;
using LFMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LFMS.Controllers;

[Authorize]
public class ChatController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public ChatController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Inbox()
    {
        var me = await _users.GetUserAsync(User);
        if (me == null) return Challenge();

        var partnerIds = await _db.ChatMessages
            .Where(x => x.SenderId == me.Id || x.ReceiverId == me.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.SenderId == me.Id ? x.ReceiverId : x.SenderId)
            .Distinct()
            .ToListAsync();

        var partners = await _users.Users
            .Where(u => partnerIds.Contains(u.Id) && u.IsActive)
            .ToListAsync();

        var ordered = partnerIds
            .Select(id => partners.FirstOrDefault(u => u.Id == id))
            .Where(u => u != null)
            .Cast<ApplicationUser>()
            .ToList();

        ViewBag.CurrentUserId = me.Id;
        return View(ordered);
    }

    [HttpGet]
    public async Task<IActionResult> With(string userId)
    {
        var me = await _users.GetUserAsync(User);
        if (me == null) return Challenge();
        if (string.IsNullOrWhiteSpace(userId) || userId == me.Id) return BadRequest("You cannot chat with yourself.");

        var other = await _users.FindByIdAsync(userId);
        if (other == null || !other.IsActive) return NotFound();

        var messages = await _db.ChatMessages
            .Where(x => (x.SenderId == me.Id && x.ReceiverId == other.Id) || (x.SenderId == other.Id && x.ReceiverId == me.Id))
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        var unread = await _db.ChatMessages.Where(x => x.SenderId == other.Id && x.ReceiverId == me.Id && !x.IsRead).ToListAsync();
        foreach (var message in unread) message.IsRead = true;
        if (unread.Count > 0) await _db.SaveChangesAsync();

        ViewBag.OtherUser = other;
        ViewBag.CurrentUserId = me.Id;
        return View(messages);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string userId, string content)
    {
        var me = await _users.GetUserAsync(User);
        if (me == null) return Challenge();
        var other = await _users.FindByIdAsync(userId);
        if (other == null || !other.IsActive || other.Id == me.Id) return NotFound();

        content = (content ?? "").Trim();
        if (string.IsNullOrWhiteSpace(content) || content.Length > 2000)
            return RedirectToAction(nameof(With), new { userId });

        _db.ChatMessages.Add(new ChatMessage { SenderId = me.Id, ReceiverId = other.Id, Content = content });
        _db.Notifications.Add(new Notification
        {
            UserId = other.Id,
            Message = $"{me.FullName} sent you a new message.",
            Link = $"/Chat/With?userId={me.Id}"
        });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(With), new { userId });
    }
}
