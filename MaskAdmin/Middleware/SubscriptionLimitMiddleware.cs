using MaskAdmin.Data;
using MaskAdmin.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MaskAdmin.Middleware;

public class SubscriptionLimitMiddleware
{
    private readonly RequestDelegate _next;

    public SubscriptionLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
        // Only check for authenticated users
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                // Load user's subscription with plan
                var subscription = await dbContext.Subscriptions
                    .Include(s => s.Plan)
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

                if (subscription != null && subscription.Plan != null)
                {
                    // Add subscription limits to HttpContext for easy access
                    context.Items["UserSubscription"] = subscription;
                    context.Items["SubscriptionPlan"] = subscription.Plan;
                    context.Items["MaxProfiles"] = subscription.Plan.MaxProfiles;
                    context.Items["MaxTeamMembers"] = subscription.Plan.MaxTeamMembers;
                    context.Items["CloudProfilesEnabled"] = subscription.Plan.CloudProfilesEnabled;
                    context.Items["TeamCollaborationEnabled"] = subscription.Plan.TeamCollaborationEnabled;
                    context.Items["ApiAccessEnabled"] = subscription.Plan.ApiAccessEnabled;
                    context.Items["ApiRequestsPerDay"] = subscription.Plan.ApiRequestsPerDay;
                    context.Items["StorageGB"] = subscription.Plan.StorageGB;

                    // Check if subscription has expired
                    if (subscription.EndDate.HasValue && subscription.EndDate.Value < DateTime.UtcNow)
                    {
                        subscription.Status = SubscriptionStatus.Expired;
                        subscription.IsActive = false;
                        await dbContext.SaveChangesAsync();

                        // Redirect to subscription expired page
                        if (!context.Request.Path.StartsWithSegments("/Subscription/Expired"))
                        {
                            context.Response.Redirect("/Subscription/Expired");
                            return;
                        }
                    }

                    // Check if subscription is past due
                    if (subscription.Status == SubscriptionStatus.PastDue
                        && !context.Request.Path.StartsWithSegments("/Subscription/PastDue"))
                    {
                        context.Response.Redirect("/Subscription/PastDue");
                        return;
                    }
                }
                else
                {
                    // User has no active subscription - create free tier
                    var freePlan = await dbContext.SubscriptionPlans
                        .FirstOrDefaultAsync(p => p.Tier == SubscriptionTier.Free);

                    if (freePlan != null)
                    {
                        var newSubscription = new Subscription
                        {
                            UserId = userId,
                            PlanId = freePlan.Id,
                            Tier = SubscriptionTier.Free,
                            MaxProfiles = freePlan.MaxProfiles,
                            Price = 0,
                            IsActive = true,
                            Status = SubscriptionStatus.Active,
                            StartDate = DateTime.UtcNow
                        };

                        dbContext.Subscriptions.Add(newSubscription);
                        await dbContext.SaveChangesAsync();

                        context.Items["UserSubscription"] = newSubscription;
                        context.Items["SubscriptionPlan"] = freePlan;
                        context.Items["MaxProfiles"] = freePlan.MaxProfiles;
                    }
                }
            }
        }

        await _next(context);
    }
}

public static class SubscriptionLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseSubscriptionLimits(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SubscriptionLimitMiddleware>();
    }
}
