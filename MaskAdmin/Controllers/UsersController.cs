using MaskAdmin.Models;
using MaskAdmin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MaskAdmin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class UsersController : Controller
{
    private readonly IUserService _userService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        ISubscriptionService subscriptionService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    // GET: /Users
    public async Task<IActionResult> Index(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? sort = null)
    {
        try
        {
            var (users, totalCount) = await _userService.GetUsersAsync(page, pageSize, search, status, sort);

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Sort = sort;

            return View(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading users");
            TempData["Error"] = "Failed to load users";
            return View(new List<User>());
        }
    }

    // GET: /Users/Details/5
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found";
                return RedirectToAction(nameof(Index));
            }

            var stats = await _userService.GetUserStatsAsync(id);
            ViewBag.Stats = stats;

            return View(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading user details");
            TempData["Error"] = "Failed to load user details";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: /Users/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, User user)
    {
        try
        {
            if (id != user.Id)
                return BadRequest();

            var success = await _userService.UpdateUserAsync(id, user);

            if (success)
            {
                TempData["Success"] = "User updated successfully";
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Error"] = "Failed to update user";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user");
            TempData["Error"] = "Failed to update user";
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // POST: /Users/AdjustBalance/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustBalance(int id, decimal amount, string reason)
    {
        try
        {
            var success = await _userService.AdjustBalanceAsync(id, amount, reason);

            if (success)
            {
                TempData["Success"] = $"Balance adjusted by {amount:C}";
            }
            else
            {
                TempData["Error"] = "Failed to adjust balance";
            }

            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adjusting balance");
            TempData["Error"] = "Failed to adjust balance";
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // POST: /Users/Ban/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ban(int id, string reason)
    {
        try
        {
            var success = await _userService.BanUserAsync(id, reason);

            if (success)
            {
                TempData["Success"] = "User banned successfully";
            }
            else
            {
                TempData["Error"] = "Failed to ban user";
            }

            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error banning user");
            TempData["Error"] = "Failed to ban user";
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // POST: /Users/Unban/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unban(int id)
    {
        try
        {
            var success = await _userService.UnbanUserAsync(id);

            if (success)
            {
                TempData["Success"] = "User unbanned successfully";
            }
            else
            {
                TempData["Error"] = "Failed to unban user";
            }

            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unbanning user");
            TempData["Error"] = "Failed to unban user";
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // POST: /Users/Freeze/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Freeze(int id, string reason)
    {
        try
        {
            var success = await _userService.FreezeUserAsync(id, reason);

            if (success)
            {
                TempData["Success"] = "User frozen successfully";
            }
            else
            {
                TempData["Error"] = "Failed to freeze user";
            }

            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error freezing user");
            TempData["Error"] = "Failed to freeze user";
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // POST: /Users/ResetPassword/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> ResetPassword(int id, string newPassword)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                TempData["Error"] = "Password must be at least 8 characters";
                return RedirectToAction(nameof(Details), new { id });
            }

            var success = await _userService.ResetPasswordAsync(id, newPassword);

            if (success)
            {
                TempData["Success"] = "Password reset successfully";
            }
            else
            {
                TempData["Error"] = "Failed to reset password";
            }

            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password");
            TempData["Error"] = "Failed to reset password";
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // POST: /Users/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var success = await _userService.DeleteUserAsync(id);

            if (success)
            {
                TempData["Success"] = "User deleted successfully";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "Failed to delete user. User may have active profiles.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user");
            TempData["Error"] = "Failed to delete user";
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // POST: /Users/AssignSubscription/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignSubscription(
        int id,
        SubscriptionTier tier,
        int maxProfiles,
        DateTime? endDate)
    {
        try
        {
            var success = await _subscriptionService.AssignSubscriptionToUserAsync(id, tier, maxProfiles, endDate);

            if (success)
            {
                TempData["Success"] = "Subscription assigned successfully";
            }
            else
            {
                TempData["Error"] = "Failed to assign subscription";
            }

            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning subscription");
            TempData["Error"] = "Failed to assign subscription";
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // GET: /Users/Logs/5
    public async Task<IActionResult> Logs(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var logs = await _userService.GetUserLogsAsync(id, page, pageSize);
            var user = await _userService.GetUserByIdAsync(id);

            ViewBag.User = user;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;

            return View(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading user logs");
            TempData["Error"] = "Failed to load logs";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
