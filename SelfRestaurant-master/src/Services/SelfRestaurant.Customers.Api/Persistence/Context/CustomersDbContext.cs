using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Customers.Api.Persistence.Entities;

namespace SelfRestaurant.Customers.Api.Persistence;

public sealed class CustomersDbContext : DbContext
{
    public CustomersDbContext(DbContextOptions<CustomersDbContext> options)
        : base(options)
    {
    }

    public DbSet<InboxEvents> InboxEvents => Set<InboxEvents>();
    public DbSet<ReadyDishNotifications> ReadyDishNotifications => Set<ReadyDishNotifications>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationPreOrderItem> ReservationPreOrderItems => Set<ReservationPreOrderItem>();
    public DbSet<ReservationTable> ReservationTables => Set<ReservationTable>();
    public DbSet<CustomerFavorite> CustomerFavorites => Set<CustomerFavorite>();
    public DbSet<CustomerReview> CustomerReviews => Set<CustomerReview>();
    public DbSet<SupportRequest> SupportRequests => Set<SupportRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboxEvents>(entity =>
        {
            entity.HasKey(e => e.InboxEventId);
            entity.HasIndex(e => new { e.Source, e.SourceEventId }).IsUnique().HasDatabaseName("UX_InboxEvents_Source_SourceEventId");
            entity.HasIndex(e => e.ReceivedAtUtc).HasDatabaseName("IX_InboxEvents_ReceivedAtUtc");
            entity.Property(e => e.Source).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.EventName).HasMaxLength(200).IsUnicode(false);
            entity.Property(e => e.CorrelationId).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.PayloadJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("PROCESSED");
            entity.Property(e => e.RetryCount).HasDefaultValue(0);
            entity.Property(e => e.ReceivedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.NextRetryAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.ProcessedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.Error).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<ReadyDishNotifications>(entity =>
        {
            entity.HasKey(e => e.ReadyDishNotificationId);
            entity.HasIndex(e => new { e.OrderId, e.OrderItemId, e.EventName }).HasDatabaseName("IX_ReadyDishNotifications_Order_Item_Event");
            entity.Property(e => e.EventName).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.DishName).HasMaxLength(200);
            entity.Property(e => e.Message).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("OPEN");
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.ResolvedAtUtc).HasColumnType("datetime2");
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(e => e.ReservationId);
            entity.HasIndex(e => e.ReservationCode).IsUnique().HasDatabaseName("UX_Reservations_ReservationCode");
            entity.HasIndex(e => new { e.BranchId, e.ReservedAt, e.Status }).HasDatabaseName("IX_Reservations_Branch_ReservedAt_Status");
            entity.HasIndex(e => e.PhoneNumber).HasDatabaseName("IX_Reservations_PhoneNumber");
            entity.HasIndex(e => e.IdempotencyKey)
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL")
                .HasDatabaseName("UX_Reservations_IdempotencyKey");
            entity.Property(e => e.ReservationCode).HasMaxLength(40).IsUnicode(false);
            entity.Property(e => e.CustomerName).HasMaxLength(200);
            entity.Property(e => e.PhoneNumber).HasMaxLength(30);
            entity.Property(e => e.ReservedAt).HasColumnType("datetime2");
            entity.Property(e => e.ArrivalWindowMinutes).HasDefaultValue(30);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Pending");
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.DiningSessionCode).HasMaxLength(64).IsUnicode(false);
            entity.Property(e => e.CheckInStartedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.CheckInIdempotencyKey).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.CheckedInAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.CancelledAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.UpdatedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.IdempotencyKey).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.RequestHash).HasMaxLength(64).IsUnicode(false);
            entity.Property(e => e.LastMutationKey).HasMaxLength(140).IsUnicode(false);
            entity.Property(e => e.LastMutationHash).HasMaxLength(64).IsUnicode(false);
            entity.Property(e => e.PreOrderSubmittedAtUtc).HasColumnType("datetime2");
            entity.HasMany(e => e.PreOrderItems)
                .WithOne(e => e.Reservation)
                .HasForeignKey(e => e.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.ReservationTables)
                .WithOne(e => e.Reservation)
                .HasForeignKey(e => e.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReservationPreOrderItem>(entity =>
        {
            entity.HasKey(e => e.ReservationItemId);
            entity.HasIndex(e => new { e.ReservationId, e.Status }).HasDatabaseName("IX_ReservationPreOrderItems_Reservation_Status");
            entity.Property(e => e.DishNameSnapshot).HasMaxLength(200);
            entity.Property(e => e.UnitPriceSnapshot).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Pending");
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.UpdatedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.ConvertedAtUtc).HasColumnType("datetime2");
        });

        modelBuilder.Entity<ReservationTable>(entity =>
        {
            entity.HasKey(e => e.ReservationTableId);
            entity.HasIndex(e => new { e.ReservationId, e.TableId }).IsUnique().HasDatabaseName("UX_ReservationTables_Reservation_Table");
            entity.HasIndex(e => e.TableId).HasDatabaseName("IX_ReservationTables_TableId");
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
        });
        modelBuilder.Entity<CustomerFavorite>(entity =>
        {
            entity.HasKey(x => x.CustomerFavoriteId);
            entity.HasIndex(x => new { x.CustomerId, x.DishId }).IsUnique();
            entity.Property(x => x.DishNameSnapshot).HasMaxLength(200);
        });
        modelBuilder.Entity<CustomerReview>(entity =>
        {
            entity.HasKey(x => x.CustomerReviewId);
            entity.HasIndex(x => x.InvoiceId).IsUnique();
            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.CreateIdempotencyKey).IsUnique();
            entity.Property(x => x.Comment).HasMaxLength(2000);
            entity.Property(x => x.ImageUrlsJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.CreateIdempotencyKey).HasMaxLength(140).IsUnicode(false);
            entity.Property(x => x.RequestHash).HasMaxLength(64).IsUnicode(false);
        });
        modelBuilder.Entity<SupportRequest>(entity =>
        {
            entity.HasKey(x => x.SupportRequestId);
            entity.HasIndex(x => x.RequestCode).IsUnique();
            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.Property(x => x.RequestCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(x => x.Category).HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.Subject).HasMaxLength(200);
            entity.Property(x => x.Message).HasMaxLength(3000);
            entity.Property(x => x.Status).HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(140).IsUnicode(false);
            entity.Property(x => x.RequestHash).HasMaxLength(64).IsUnicode(false);
        });
    }
}
