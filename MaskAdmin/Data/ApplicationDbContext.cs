using MaskAdmin.Models;
using Microsoft.EntityFrameworkCore;

namespace MaskAdmin.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            return;
        }
        
        // Note: PendingModelChangesWarning is only available in EF Core 9.0+
        // Suppress model validation errors in development
        #if DEBUG
        optionsBuilder.EnableSensitiveDataLogging();
        #endif
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public DbSet<PlanFeature> PlanFeatures { get; set; }
    public DbSet<BrowserProfile> BrowserProfiles { get; set; }
    public DbSet<ServerNode> ServerNodes { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<SystemSettings> SystemSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.LastLoginAt);
            entity.HasIndex(e => e.IsBanned);
            entity.HasIndex(e => e.IsFrozen);

            entity.Property(e => e.IsBanned).HasDefaultValue(false);
            entity.Property(e => e.IsFrozen).HasDefaultValue(false);
        });

        // Subscription configuration
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasIndex(e => e.EndDate);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StripeSubscriptionId);
        });

        // SubscriptionPlan configuration
        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Tier).IsUnique();
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.SortOrder);
        });

        // PlanFeature configuration
        modelBuilder.Entity<PlanFeature>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PlanId);
            entity.HasIndex(e => e.SortOrder);
        });

        // BrowserProfile configuration
        modelBuilder.Entity<BrowserProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ServerNodeId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });

        // ServerNode configuration
        modelBuilder.Entity<ServerNode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IpAddress).IsUnique();
            entity.HasIndex(e => e.IsHealthy);
            entity.HasIndex(e => e.IsEnabled);
        });

        // Payment configuration
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Provider);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.TransactionId);
        });

        // AuditLog configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Level);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Action);
        });

        // SystemSettings configuration
        modelBuilder.Entity<SystemSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.HasIndex(e => e.Category);
        });

        // Seed default admin user
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Default admin user
        modelBuilder.Entity<User>().HasData(new User
        {
            Id = 1,
            Username = "admin",
            Email = "admin@maskbrowser.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            IsActive = true,
            IsAdmin = true,
            CreatedAt = DateTime.UtcNow
        });

        // Default subscription plans
        modelBuilder.Entity<SubscriptionPlan>().HasData(
            new SubscriptionPlan
            {
                Id = 1,
                Tier = SubscriptionTier.Free,
                Name = "Free",
                Description = "Perfect for trying out MaskBrowser",
                MonthlyPrice = 0,
                YearlyPrice = 0,
                MaxProfiles = 1,
                MaxTeamMembers = 1,
                StorageGB = 5,
                IsActive = true,
                SortOrder = 0
            },
            new SubscriptionPlan
            {
                Id = 2,
                Tier = SubscriptionTier.Starter,
                Name = "Starter",
                Description = "Great for freelancers and small projects",
                MonthlyPrice = 9.99m,
                YearlyPrice = 99.99m,
                MaxProfiles = 5,
                MaxTeamMembers = 1,
                StorageGB = 20,
                AdvancedFingerprintsEnabled = true,
                IsActive = true,
                IsPopular = false,
                SortOrder = 1
            },
            new SubscriptionPlan
            {
                Id = 3,
                Tier = SubscriptionTier.Pro,
                Name = "Pro",
                Description = "For professionals managing multiple accounts",
                MonthlyPrice = 29.99m,
                YearlyPrice = 299.99m,
                MaxProfiles = 20,
                MaxTeamMembers = 3,
                CloudProfilesEnabled = true,
                TeamCollaborationEnabled = true,
                PrioritySupport = true,
                AdvancedFingerprintsEnabled = true,
                ApiAccessEnabled = true,
                ApiRequestsPerDay = 10000,
                StorageGB = 100,
                IsActive = true,
                IsPopular = true,
                SortOrder = 2
            },
            new SubscriptionPlan
            {
                Id = 4,
                Tier = SubscriptionTier.Business,
                Name = "Business",
                Description = "For teams and growing businesses",
                MonthlyPrice = 99.99m,
                YearlyPrice = 999.99m,
                MaxProfiles = 100,
                MaxTeamMembers = 10,
                CloudProfilesEnabled = true,
                TeamCollaborationEnabled = true,
                PrioritySupport = true,
                AdvancedFingerprintsEnabled = true,
                ApiAccessEnabled = true,
                ApiRequestsPerDay = 100000,
                CustomBrandingEnabled = true,
                StorageGB = 500,
                IsActive = true,
                SortOrder = 3
            },
            new SubscriptionPlan
            {
                Id = 5,
                Tier = SubscriptionTier.Enterprise,
                Name = "Enterprise",
                Description = "Custom solutions for large organizations",
                MonthlyPrice = 299.99m,
                YearlyPrice = 2999.99m,
                MaxProfiles = 1000,
                MaxTeamMembers = 50,
                CloudProfilesEnabled = true,
                TeamCollaborationEnabled = true,
                PrioritySupport = true,
                AdvancedFingerprintsEnabled = true,
                ApiAccessEnabled = true,
                ApiRequestsPerDay = 1000000,
                CustomBrandingEnabled = true,
                DedicatedAccountManagerEnabled = true,
                StorageGB = 2000,
                IsActive = true,
                SortOrder = 4
            }
        );

        // Default subscription for admin
        modelBuilder.Entity<Subscription>().HasData(new Subscription
        {
            Id = 1,
            UserId = 1,
            PlanId = 5,
            Tier = SubscriptionTier.Enterprise,
            MaxProfiles = 1000,
            Price = 0,
            IsActive = true,
            StartDate = DateTime.UtcNow,
            Status = SubscriptionStatus.Active
        });

        // Default system settings
        modelBuilder.Entity<SystemSettings>().HasData(
            new SystemSettings
            {
                Id = 1,
                Key = "SystemName",
                Value = "MASK BROWSER Admin",
                Category = "General",
                Description = "System display name"
            },
            new SystemSettings
            {
                Id = 2,
                Key = "AdminEmail",
                Value = "admin@maskbrowser.com",
                Category = "General",
                Description = "Administrator email address"
            },
            new SystemSettings
            {
                Id = 3,
                Key = "MaxProfilesPerUser",
                Value = "100",
                Category = "Limits",
                Description = "Maximum profiles per user (default)"
            },
            new SystemSettings
            {
                Id = 4,
                Key = "MaxContainersPerNode",
                Value = "1000",
                Category = "Server",
                Description = "Maximum containers per server node"
            },
            new SystemSettings
            {
                Id = 5,
                Key = "HealthCheckIntervalSeconds",
                Value = "30",
                Category = "Server",
                Description = "Health check interval in seconds"
            }
        );
    }
}
