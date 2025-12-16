using MaskAdmin.Data;
using MaskAdmin.Models;
using MaskAdmin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MaskAdmin.Controllers;

[Authorize]
public class SubscriptionPlanController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogService _logService;

    public SubscriptionPlanController(
        ApplicationDbContext context,
        ISubscriptionService subscriptionService,
        ILogService logService)
    {
        _context = context;
        _subscriptionService = subscriptionService;
        _logService = logService;
    }

    public async Task<IActionResult> Index()
    {
        var plans = await _subscriptionService.GetAllActivePlansAsync();
        return View(plans);
    }

    public async Task<IActionResult> Details(int id)
    {
        var plan = await _context.SubscriptionPlans
            .Include(p => p.Subscriptions)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (plan == null)
            return NotFound();

        return View(plan);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SubscriptionPlan plan)
    {
        if (ModelState.IsValid)
        {
            try
            {
                _context.SubscriptionPlans.Add(plan);
                await _context.SaveChangesAsync();

                await _logService.LogAsync(
                    LogCategory.SubscriptionManagement,
                    LogLevel.Info,
                    "Subscription Plan Created",
                    $"Created plan: {plan.Name} ({plan.Tier})"
                );

                TempData["Success"] = "Subscription plan created successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(
                    LogCategory.SubscriptionManagement,
                    LogLevel.Error,
                    "Subscription Plan Creation Failed",
                    ex.Message
                );

                TempData["Error"] = "Failed to create subscription plan.";
            }
        }

        return View(plan);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var plan = await _context.SubscriptionPlans.FindAsync(id);
        if (plan == null)
            return NotFound();

        return View(plan);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SubscriptionPlan plan)
    {
        if (id != plan.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                plan.UpdatedAt = DateTime.UtcNow;
                _context.Update(plan);
                await _context.SaveChangesAsync();

                await _logService.LogAsync(
                    LogCategory.SubscriptionManagement,
                    LogLevel.Info,
                    "Subscription Plan Updated",
                    $"Updated plan: {plan.Name} ({plan.Tier})"
                );

                TempData["Success"] = "Subscription plan updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await PlanExists(plan.Id))
                    return NotFound();
                else
                    throw;
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(
                    LogCategory.SubscriptionManagement,
                    LogLevel.Error,
                    "Subscription Plan Update Failed",
                    ex.Message
                );

                TempData["Error"] = "Failed to update subscription plan.";
            }
        }

        return View(plan);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var plan = await _context.SubscriptionPlans.FindAsync(id);
        if (plan == null)
            return NotFound();

        try
        {
            // Don't delete, just deactivate
            plan.IsActive = false;
            plan.UpdatedAt = DateTime.UtcNow;
            _context.Update(plan);
            await _context.SaveChangesAsync();

            await _logService.LogAsync(
                LogCategory.SubscriptionManagement,
                LogLevel.Info,
                "Subscription Plan Deactivated",
                $"Deactivated plan: {plan.Name} ({plan.Tier})"
            );

            TempData["Success"] = "Subscription plan deactivated successfully.";
        }
        catch (Exception ex)
        {
            await _logService.LogAsync(
                LogCategory.SubscriptionManagement,
                LogLevel.Error,
                "Subscription Plan Deactivation Failed",
                ex.Message
            );

            TempData["Error"] = "Failed to deactivate subscription plan.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> PlanExists(int id)
    {
        return await _context.SubscriptionPlans.AnyAsync(e => e.Id == id);
    }
}
