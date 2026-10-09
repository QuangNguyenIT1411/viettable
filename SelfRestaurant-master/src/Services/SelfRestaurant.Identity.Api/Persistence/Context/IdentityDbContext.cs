using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Identity.Api.Persistence.Entities;

namespace SelfRestaurant.Identity.Api.Persistence;

public sealed class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<BusinessAuditLogs> BusinessAuditLogs => Set<BusinessAuditLogs>();
    public DbSet<CatalogBranchSnapshots> CatalogBranchSnapshots => Set<CatalogBranchSnapshots>();
    public DbSet<CustomerLoyalty> CustomerLoyalty => Set<CustomerLoyalty>();
    public DbSet<Customers> Customers => Set<Customers>();
    public DbSet<EmployeeRoles> EmployeeRoles => Set<EmployeeRoles>();
    public DbSet<Employees> Employees => Set<Employees>();
    public DbSet<PasswordResetTokens> PasswordResetTokens => Set<PasswordResetTokens>();
    public DbSet<MobileRefreshToken> MobileRefreshTokens => Set<MobileRefreshToken>();
    public DbSet<CustomerSettings> CustomerSettings => Set<CustomerSettings>();
    public DbSet<CloudDataProtectionKey> CloudDataProtectionKeys => Set<CloudDataProtectionKey>();
    public DbSet<CloudStaffResetToken> CloudStaffResetTokens => Set<CloudStaffResetToken>();
    public DbSet<CloudStaffRevocation> CloudStaffRevocations => Set<CloudStaffRevocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CloudStaffRevocation>(entity =>
        {
            entity.ToTable("CloudStaffRevocations");
            entity.HasKey(x => x.TicketHash);
            entity.Property(x => x.TicketHash).HasMaxLength(64);
            entity.HasIndex(x => x.ExpiresAtUtc);
        });
        modelBuilder.Entity<CloudStaffResetToken>(entity =>
        {
            entity.ToTable("CloudStaffResetTokens");
            entity.HasKey(x => x.TokenHash);
            entity.Property(x => x.TokenHash).HasMaxLength(64);
            entity.HasOne<Employees>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CloudDataProtectionKey>(entity =>
        {
            entity.ToTable("CloudDataProtectionKeys");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FriendlyName).HasMaxLength(200);
        });
        // PostgreSQL overrides are applied after the existing SQL Server mapping.
        modelBuilder.Entity<LoyaltySettlement>(entity =>
        {
            entity.ToTable("LoyaltySettlements");
            entity.HasKey(x => x.SettlementKey);
            entity.Property(x => x.SettlementKey).HasMaxLength(100);
            entity.Property(x => x.AmountPaid).HasPrecision(18, 2);
        });
        modelBuilder.Entity<BusinessAuditLogs>(entity =>
        {
            entity.HasKey(e => e.BusinessAuditLogId);
            entity.HasIndex(e => e.CreatedAtUtc).HasDatabaseName("IX_BusinessAuditLogs_CreatedAtUtc");
            entity.HasIndex(e => new { e.EntityType, e.EntityId }).HasDatabaseName("IX_BusinessAuditLogs_Entity");
            entity.HasIndex(e => e.CustomerId).HasDatabaseName("IX_BusinessAuditLogs_CustomerId");
            entity.HasIndex(e => e.EmployeeId).HasDatabaseName("IX_BusinessAuditLogs_EmployeeId");
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.ActionType).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.EntityType).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.EntityId).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.ActorType).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.ActorCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.ActorName).HasMaxLength(100);
            entity.Property(e => e.ActorRoleCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.IpAddress).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.UserAgent).HasMaxLength(500).IsUnicode(false);
            entity.Property(e => e.CorrelationId).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Notes).HasMaxLength(1000);
        });

        modelBuilder.Entity<CatalogBranchSnapshots>(entity =>
        {
            entity.HasKey(e => e.BranchId);
            entity.HasIndex(e => e.IsActive).HasDatabaseName("idx_catalog_branch_snapshots_active");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Location).HasMaxLength(500);
            entity.Property(e => e.RefreshedAtUtc).HasColumnType("datetime2");
        });

        modelBuilder.Entity<Customers>(entity =>
        {
            entity.HasKey(e => e.CustomerID);
            entity.HasIndex(e => e.Username).IsUnique().HasDatabaseName("UQ_Customers_Username");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("idx_customers_active");
            entity.HasIndex(e => new { e.ExternalProvider, e.ExternalSubject }).HasDatabaseName("idx_customers_external_login");
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.AuthProvider).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Password");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.ExternalProvider).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.ExternalSubject).HasMaxLength(120).IsUnicode(false);
            entity.Property(e => e.Gender).HasMaxLength(10);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LoyaltyPoints).HasDefaultValue(0);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Password).HasMaxLength(255).IsUnicode(false).IsRequired(false);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Username).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<CustomerLoyalty>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("CustomerLoyalty");

            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<EmployeeRoles>(entity =>
        {
            entity.HasKey(e => e.RoleID);
            entity.HasIndex(e => e.RoleCode).IsUnique();
            entity.Property(e => e.RoleCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.RoleName).HasMaxLength(100);
        });

        modelBuilder.Entity<Employees>(entity =>
        {
            entity.HasKey(e => e.EmployeeID);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.IsActive).HasDatabaseName("idx_employees_active");
            entity.HasIndex(e => e.BranchID).HasDatabaseName("idx_employees_branch");
            entity.HasIndex(e => e.RoleID).HasDatabaseName("idx_employees_role");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Password).HasMaxLength(255).IsUnicode(false);
            entity.Property(e => e.Phone).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Salary).HasColumnType("decimal(15, 2)");
            entity.Property(e => e.Shift).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Username).HasMaxLength(50).IsUnicode(false);
            entity.HasOne(e => e.Role)
                .WithMany(r => r.Employees)
                .HasForeignKey(e => e.RoleID)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<PasswordResetTokens>(entity =>
        {
            entity.HasKey(e => e.TokenID);
            entity.HasIndex(e => e.CustomerID).HasDatabaseName("idx_customer");
            entity.HasIndex(e => e.Token).HasDatabaseName("idx_token");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ExpiryDate).HasColumnType("datetime");
            entity.Property(e => e.Token).HasMaxLength(255).IsUnicode(false);
            entity.HasOne(e => e.Customer)
                .WithMany(c => c.PasswordResetTokens)
                .HasForeignKey(e => e.CustomerID);
        });

        modelBuilder.Entity<MobileRefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique().HasDatabaseName("UQ_MobileRefreshTokens_TokenHash");
            entity.HasIndex(e => new { e.CustomerId, e.ExpiresAtUtc }).HasDatabaseName("IX_MobileRefreshTokens_Customer_Expiry");
            entity.HasIndex(e => e.FamilyId).HasDatabaseName("IX_MobileRefreshTokens_Family");
            entity.Property(e => e.TokenHash).HasMaxLength(64).IsUnicode(false);
            entity.Property(e => e.FamilyId).HasMaxLength(36).IsUnicode(false);
            entity.Property(e => e.ReplacedByTokenHash).HasMaxLength(64).IsUnicode(false);
            entity.Property(e => e.RevokedReason).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.CreatedByIp).HasMaxLength(64).IsUnicode(false);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CustomerSettings>(entity =>
        {
            entity.HasKey(e => e.CustomerId);
            entity.Property(e => e.LanguageCode).HasMaxLength(10).IsUnicode(false).HasDefaultValue("vi-VN");
            entity.Property(e => e.PushNotificationsEnabled).HasDefaultValue(true);
            entity.Property(e => e.SmsReservationRemindersEnabled).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAtUtc).HasColumnType("datetime2");
            entity.HasOne(e => e.Customer)
                .WithOne()
                .HasForeignKey<CustomerSettings>(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        if (Database.IsNpgsql())
        {
            // Preserve case-insensitive account/email lookup and username uniqueness.
            // citext is database infrastructure, not a cross-service domain table.
            modelBuilder.HasPostgresExtension("citext");
            modelBuilder.Entity<Customers>().Property(x => x.Username).HasColumnType("citext");
            modelBuilder.Entity<Customers>().Property(x => x.Email).HasColumnType("citext");
            modelBuilder.Entity<Employees>().Property(x => x.Username).HasColumnType("citext");
            modelBuilder.Entity<Employees>().Property(x => x.Email).HasColumnType("citext");
            modelBuilder.Entity<LoyaltyCard>(entity =>
            {
                entity.ToTable("LoyaltyCards");
                entity.HasKey(x => x.CardID);
                entity.HasOne<Customers>().WithMany().HasForeignKey(x => x.CustomerID);
            });
            SelfRestaurant.CloudCompatibility.PostgresModel.Apply(modelBuilder, "identity");
        }
    }
}
