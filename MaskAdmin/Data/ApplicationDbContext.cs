using MaskAdmin.Models;
using Microsoft.EntityFrameworkCore;

namespace MaskAdmin.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
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
        });

        // Subscription configuration
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasIndex(e => e.EndDate);
            entity.HasIndex(e => e.IsActive);
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

        // Default subscription for admin
        modelBuilder.Entity<Subscription>().HasData(new Subscription
        {
            Id = 1,
            UserId = 1,
            Tier = SubscriptionTier.Enterprise,
            MaxProfiles = 1000,
            Price = 0,
            IsActive = true,
            StartDate = DateTime.UtcNow
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
