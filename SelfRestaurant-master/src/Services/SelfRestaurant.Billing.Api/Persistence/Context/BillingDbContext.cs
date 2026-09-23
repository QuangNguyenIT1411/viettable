using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Billing.Api.Persistence;

public sealed class BillingDbContext : DbContext
{
    public BillingDbContext(DbContextOptions<BillingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Bills> Bills => Set<Bills>();
    public DbSet<BusinessAuditLogs> BusinessAuditLogs => Set<BusinessAuditLogs>();
    public DbSet<CheckoutCommands> CheckoutCommands => Set<CheckoutCommands>();
    public DbSet<OrderContextSnapshots> OrderContextSnapshots => Set<OrderContextSnapshots>();
    public DbSet<OutboxEvents> OutboxEvents => Set<OutboxEvents>();
    public DbSet<CheckoutQuote> CheckoutQuotes => Set<CheckoutQuote>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<SplitBill> SplitBills => Set<SplitBill>();
    public DbSet<SplitBillPart> SplitBillParts => Set<SplitBillPart>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bills>(entity =>
        {
            entity.ToTable("Bills", "billing");
            entity.HasKey(e => e.BillID);
            entity.HasIndex(e => e.BillTime).HasDatabaseName("IX_Bills_BillTime");
            entity.HasIndex(e => e.CheckoutIdempotencyKey).IsUnique().HasFilter("[CheckoutIdempotencyKey] IS NOT NULL").HasDatabaseName("UX_Bills_CheckoutIdempotencyKey");
            entity.HasIndex(e => e.OrderID).IsUnique().HasFilter("[IsActive] = 1").HasDatabaseName("UX_Bills_OrderID_Active");
            entity.HasIndex(e => e.OrderID).HasDatabaseName("IX_Bills_OrderID");
            entity.Property(e => e.BillCode).HasMaxLength(50);
            entity.Property(e => e.CheckoutIdempotencyKey).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.DiningSessionCode).HasMaxLength(64).IsUnicode(false);
            entity.Property(e => e.OrderCodeSnapshot).HasMaxLength(50);
            entity.Property(e => e.TableNameSnapshot).HasMaxLength(200);
            entity.Property(e => e.BranchNameSnapshot).HasMaxLength(200);
            entity.Property(e => e.BillTime).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ChangeAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Discount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PaymentAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PaymentMethod).HasMaxLength(20);
            entity.Property(e => e.PointsDiscount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Subtotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<BusinessAuditLogs>(entity =>
        {
            entity.HasKey(e => e.BusinessAuditLogId);
            entity.HasIndex(e => e.CreatedAtUtc).HasDatabaseName("IX_BusinessAuditLogs_CreatedAtUtc");
            entity.HasIndex(e => new { e.EntityType, e.EntityId }).HasDatabaseName("IX_BusinessAuditLogs_Entity");
            entity.HasIndex(e => e.OrderId).HasDatabaseName("IX_BusinessAuditLogs_OrderId");
            entity.HasIndex(e => e.BillId).HasDatabaseName("IX_BusinessAuditLogs_BillId");
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.ActionType).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.EntityType).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.EntityId).HasMaxLength(100);
            entity.Property(e => e.ActorType).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.ActorCode).HasMaxLength(100);
            entity.Property(e => e.ActorName).HasMaxLength(200);
            entity.Property(e => e.ActorRoleCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.DiningSessionCode).HasMaxLength(64).IsUnicode(false);
            entity.Property(e => e.CorrelationId).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.BeforeState).HasColumnType("nvarchar(max)");
            entity.Property(e => e.AfterState).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<CheckoutCommands>(entity =>
        {
            entity.HasKey(e => e.CheckoutCommandId);
            entity.HasIndex(e => e.IdempotencyKey).IsUnique().HasDatabaseName("UX_CheckoutCommands_IdempotencyKey");
            entity.HasIndex(e => e.CreatedAtUtc).HasDatabaseName("IX_CheckoutCommands_CreatedAtUtc");
            entity.Property(e => e.BillCode).HasMaxLength(50);
            entity.Property(e => e.CustomerName).HasMaxLength(200);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.DiningSessionCode).HasMaxLength(64).IsUnicode(false);
            entity.Property(e => e.Status).HasMaxLength(20).IsUnicode(false).HasDefaultValue("PENDING");
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.CompletedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.Error).HasColumnType("nvarchar(max)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ChangeAmount).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<OutboxEvents>(entity =>
        {
            entity.HasKey(e => e.OutboxEventId);
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_OutboxEvents_Status");
            entity.HasIndex(e => e.CreatedAtUtc).HasDatabaseName("IX_OutboxEvents_CreatedAtUtc");
            entity.Property(e => e.EventName).HasMaxLength(200).IsUnicode(false);
            entity.Property(e => e.Source).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.CorrelationId).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.PayloadJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("PENDING");
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.OccurredAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.ProcessedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.Error).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<OrderContextSnapshots>(entity =>
        {
            entity.HasKey(e => e.OrderId);
            entity.HasIndex(e => e.BranchId).HasDatabaseName("IX_OrderContextSnapshots_BranchId");
            entity.HasIndex(e => e.RefreshedAtUtc).HasDatabaseName("IX_OrderContextSnapshots_RefreshedAtUtc");
            entity.Property(e => e.OrderCode).HasMaxLength(50);
            entity.Property(e => e.TableName).HasMaxLength(200);
            entity.Property(e => e.BranchName).HasMaxLength(200);
            entity.Property(e => e.RefreshedAtUtc).HasColumnType("datetime2");
        });

        modelBuilder.Entity<CheckoutQuote>(entity =>
        {
            entity.HasKey(x => x.CheckoutQuoteId);
            entity.HasIndex(x => x.QuoteCode).IsUnique();
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasIndex(x => x.DiningSessionId);
            entity.Property(x => x.QuoteCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(x => x.DiningSessionCode).HasMaxLength(64).IsUnicode(false);
            entity.Property(x => x.Status).HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(140).IsUnicode(false);
            entity.Property(x => x.RequestHash).HasMaxLength(64).IsUnicode(false);
            entity.Property(x => x.ItemsJson).HasColumnType("nvarchar(max)");
            foreach (var p in new[] { nameof(CheckoutQuote.Subtotal), nameof(CheckoutQuote.VoucherDiscount), nameof(CheckoutQuote.PointsDiscount), nameof(CheckoutQuote.TotalAmount) })
                entity.Property(p).HasColumnType("decimal(18,2)");
        });
        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(x => x.VoucherId); entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(50).IsUnicode(false); entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.DiscountType).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.DiscountValue).HasColumnType("decimal(18,2)"); entity.Property(x => x.MinSubtotal).HasColumnType("decimal(18,2)"); entity.Property(x => x.MaxDiscount).HasColumnType("decimal(18,2)");
        });
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("MobilePayments");
            entity.HasKey(x => x.PaymentId); entity.HasIndex(x => x.PaymentCode).IsUnique(); entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasIndex(x => x.CheckoutQuoteId); entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.HasIndex(x => x.ProviderReference).IsUnique().HasFilter("[ProviderReference] IS NOT NULL");
            entity.Property(x => x.PaymentCode).HasMaxLength(50).IsUnicode(false); entity.Property(x => x.Channel).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Method).HasMaxLength(20).IsUnicode(false); entity.Property(x => x.Location).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Status).HasMaxLength(30).IsUnicode(false); entity.Property(x => x.IdempotencyKey).HasMaxLength(140).IsUnicode(false);
            entity.Property(x => x.RequestHash).HasMaxLength(64).IsUnicode(false); entity.Property(x => x.ProviderReference).HasMaxLength(100); entity.Property(x => x.ProviderName).HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.ProviderTransactionId).HasMaxLength(100); entity.Property(x => x.ProviderPaymentUrl).HasMaxLength(1000); entity.Property(x => x.Currency).HasMaxLength(3).IsUnicode(false); entity.Property(x => x.FailureCode).HasMaxLength(100).IsUnicode(false);
        });
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(x => x.InvoiceId); entity.HasIndex(x => x.InvoiceCode).IsUnique(); entity.HasIndex(x => x.CheckoutQuoteId).IsUnique(); entity.HasIndex(x => x.DiningSessionId).IsUnique();
            entity.Property(x => x.InvoiceCode).HasMaxLength(50).IsUnicode(false); entity.Property(x => x.ItemsJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.Subtotal).HasColumnType("decimal(18,2)"); entity.Property(x => x.Discount).HasColumnType("decimal(18,2)"); entity.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
        });
        modelBuilder.Entity<SplitBill>(entity =>
        {
            entity.HasKey(x => x.SplitBillId); entity.HasIndex(x => x.IdempotencyKey).IsUnique(); entity.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Status).HasMaxLength(30).IsUnicode(false); entity.Property(x => x.IdempotencyKey).HasMaxLength(140).IsUnicode(false); entity.Property(x => x.RequestHash).HasMaxLength(64).IsUnicode(false);
            entity.HasMany(x => x.Parts).WithOne(x => x.SplitBill).HasForeignKey(x => x.SplitBillId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<SplitBillPart>(entity =>
        {
            entity.HasKey(x => x.SplitBillPartId); entity.Property(x => x.Label).HasMaxLength(100); entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false); entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        });

    }
}
