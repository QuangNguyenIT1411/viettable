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
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<ReservationPreOrderItem> ReservationPreOrderItems => Set<ReservationPreOrderItem>();
    public DbSet<ReservationTable> ReservationTables => Set<ReservationTable>();
    public DbSet<CustomerFavorite> CustomerFavorites => Set<CustomerFavorite>();
    public DbSet<CustomerReview> CustomerReviews => Set<CustomerReview>();
    public DbSet<CustomerReviewDishRating> CustomerReviewDishRatings => Set<CustomerReviewDishRating>();
    public DbSet<SupportRequest> SupportRequests => Set<SupportRequest>();
    public DbSet<SupportConversation> SupportConversations => Set<SupportConversation>();
    public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();
    public DbSet<SupportQuickReply> SupportQuickReplies => Set<SupportQuickReply>();
    public DbSet<SupportBranchHours> SupportBranchHours => Set<SupportBranchHours>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // PostgreSQL overrides are applied after the existing SQL Server mapping.
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
            entity.Property(e => e.StatusChangedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.StatusReason).HasMaxLength(300);
            entity.Property(e => e.DepositAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.DepositDueAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.ArrivalConfirmation).HasMaxLength(20).IsUnicode(false).HasDefaultValue("None");
            entity.Property(e => e.ArrivalConfirmedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.HoldExpiresAtUtc).HasColumnType("datetime2");
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
        modelBuilder.Entity<CustomerReviewDishRating>(entity =>
        {
            entity.HasKey(x => x.CustomerReviewDishRatingId);
            entity.HasIndex(x => new { x.CustomerReviewId, x.DishId }).IsUnique().HasDatabaseName("UX_CustomerReviewDishRatings_Review_Dish");
            entity.HasIndex(x => x.DishId).HasDatabaseName("IX_CustomerReviewDishRatings_DishId");
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
            entity.Property(x => x.TagsJson).HasMaxLength(500).HasDefaultValue("[]");
            entity.Property(x => x.ReplyText).HasMaxLength(1000);
            entity.Property(x => x.AuthorNameSnapshot).HasMaxLength(200);
            entity.Property(x => x.RepliedByName).HasMaxLength(200);
            entity.Property(x => x.HiddenReason).HasMaxLength(300);
            entity.Property(x => x.HiddenByName).HasMaxLength(200);
            entity.HasIndex(x => new { x.BranchId, x.IsHidden, x.CreatedAtUtc }).HasDatabaseName("IX_CustomerReviews_Branch_Hidden_Created");
            entity.HasMany(x => x.DishRatings).WithOne(x => x.Review).HasForeignKey(x => x.CustomerReviewId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.CreateIdempotencyKey).HasMaxLength(140).IsUnicode(false);
            entity.Property(x => x.RequestHash).HasMaxLength(64).IsUnicode(false);
        });
        modelBuilder.Entity<WaitlistEntry>(entity =>
        {
            entity.ToTable("ReservationWaitlist");
            entity.HasKey(e => e.WaitlistEntryId);
            entity.HasIndex(e => new { e.BranchId, e.Status, e.CreatedAtUtc }).HasDatabaseName("IX_ReservationWaitlist_Branch_Status_Created");
            entity.HasIndex(e => new { e.CustomerId, e.Status }).HasDatabaseName("IX_ReservationWaitlist_Customer_Status");
            entity.Property(e => e.ContactName).HasMaxLength(200);
            entity.Property(e => e.PhoneNumber).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Waiting");
            entity.Property(e => e.ClosedReason).HasMaxLength(300);
            entity.Property(e => e.WindowStartUtc).HasColumnType("datetime2");
            entity.Property(e => e.WindowEndUtc).HasColumnType("datetime2");
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.UpdatedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.OfferedReservedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.OfferedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.OfferExpiresAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.AcceptedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.ClosedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<SupportConversation>(entity =>
        {
            entity.ToTable("SupportConversations");
            entity.HasKey(e => e.SupportConversationId);
            entity.HasIndex(e => new { e.BranchId, e.Status, e.WaitingSinceUtc }).HasDatabaseName("IX_SupportConversations_Branch_Status_Waiting");
            entity.HasIndex(e => new { e.CustomerId, e.LastMessageAtUtc }).HasDatabaseName("IX_SupportConversations_Customer_Last");
            entity.Property(e => e.CustomerNameSnapshot).HasMaxLength(200);
            entity.Property(e => e.Topic).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.ContextType).HasMaxLength(20).IsUnicode(false).HasDefaultValue("NONE");
            entity.Property(e => e.ContextLabel).HasMaxLength(300);
            entity.Property(e => e.Status).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Waiting");
            entity.Property(e => e.ResolvedByName).HasMaxLength(200);
            entity.Property(e => e.WaitingSinceUtc).HasColumnType("datetime2");
            entity.Property(e => e.LastMessageAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.ResolvedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.UpdatedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<SupportMessage>(entity =>
        {
            entity.ToTable("SupportMessages");
            entity.HasKey(e => e.SupportMessageId);
            entity.HasIndex(e => new { e.SupportConversationId, e.SupportMessageId }).HasDatabaseName("IX_SupportMessages_Conversation_Id");
            entity.Property(e => e.SenderType).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.SenderName).HasMaxLength(200);
            entity.Property(e => e.Body).HasMaxLength(1000);
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2");
        });

        modelBuilder.Entity<SupportQuickReply>(entity =>
        {
            entity.ToTable("SupportQuickReplies");
            entity.HasKey(e => e.SupportQuickReplyId);
            entity.HasIndex(e => new { e.BranchId, e.IsActive, e.SortOrder }).HasDatabaseName("IX_SupportQuickReplies_Branch");
            entity.Property(e => e.Title).HasMaxLength(100);
            entity.Property(e => e.Body).HasMaxLength(1000);
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.UpdatedAtUtc).HasColumnType("datetime2");
        });

        modelBuilder.Entity<SupportBranchHours>(entity =>
        {
            entity.ToTable("SupportBranchHours");
            entity.HasKey(e => e.BranchId);
            entity.Property(e => e.BranchId).ValueGeneratedNever();
            entity.Property(e => e.OpenFrom).HasMaxLength(5).IsUnicode(false);
            entity.Property(e => e.OpenTo).HasMaxLength(5).IsUnicode(false);
            entity.Property(e => e.UpdatedByName).HasMaxLength(200);
            entity.Property(e => e.UpdatedAtUtc).HasColumnType("datetime2");
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
        if (Database.IsNpgsql())
            SelfRestaurant.CloudCompatibility.PostgresModel.Apply(modelBuilder, "customers");
    }
}
