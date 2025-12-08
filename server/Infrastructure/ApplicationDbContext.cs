using Microsoft.EntityFrameworkCore;
using MaskBrowser.Server.Models;

namespace MaskBrowser.Server.Infrastructure;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<BrowserProfile> BrowserProfiles { get; set; }
    public DbSet<ServerNode> ServerNodes { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<PaymentMethod> PaymentMethods { get; set; }
    public DbSet<DepositRequest> DepositRequests { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
        });

        // BrowserProfile configuration
        // BrowserProfile configuration
        modelBuilder.Entity<BrowserProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasOne(e => e.User)
                .WithMany(u => u.BrowserProfiles)
                .HasForeignKey(e => e.UserId);

            // Встроенный объект BrowserConfig
            entity.OwnsOne(e => e.Config);
        });


        // ServerNode configuration
        modelBuilder.Entity<ServerNode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IpAddress).IsUnique();
        });

        // Subscription configuration
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User)
                  .WithOne(u => u.Subscription)
                  .HasForeignKey<Subscription>(e => e.UserId);
        });

        // Payment configuration
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Payments)
                  .HasForeignKey(e => e.UserId);
            entity.HasIndex(e => e.DepositRequestId);
            entity.HasIndex(e => e.PaymentMethodId);
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.IsEnabled, e.SortOrder });
            entity.Property(e => e.ProcessorConfig).HasColumnType("jsonb");
            entity.Property(e => e.MinAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MaxAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.FeePercent).HasColumnType("decimal(5,2)");
            entity.Property(e => e.FeeFixed).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.Property(e => e.CodeExpirationMinutes).HasDefaultValue(30);
        });

        modelBuilder.Entity<DepositRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PaymentCode).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.Status });
            entity.HasIndex(e => e.ExpiresAt);

            entity.Property(e => e.PaymentCode).HasMaxLength(50);
            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.ExpectedAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ActualAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ProcessorResponse).HasColumnType("jsonb");

            entity.HasOne(e => e.User)
                .WithMany(u => u.DepositRequests)
                .HasForeignKey(e => e.UserId);

            entity.HasOne(e => e.PaymentMethod)
                .WithMany(pm => pm.DepositRequests)
                .HasForeignKey(e => e.PaymentMethodId);
        });
    }
}

