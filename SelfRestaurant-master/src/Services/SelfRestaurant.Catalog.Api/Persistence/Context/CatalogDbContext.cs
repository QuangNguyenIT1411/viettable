using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Catalog.Api.Persistence.Entities;

namespace SelfRestaurant.Catalog.Api.Persistence;

public sealed class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
        : base(options)
    {
    }

    public DbSet<Branches> Branches => Set<Branches>();
    public DbSet<BusinessAuditLogs> BusinessAuditLogs => Set<BusinessAuditLogs>();
    public DbSet<Categories> Categories => Set<Categories>();
    public DbSet<CategoryDish> CategoryDish => Set<CategoryDish>();
    public DbSet<DiningTables> DiningTables => Set<DiningTables>();
    public DbSet<DishIngredients> DishIngredients => Set<DishIngredients>();
    public DbSet<Dishes> Dishes => Set<Dishes>();
    public DbSet<IngredientBatches> IngredientBatches => Set<IngredientBatches>();
    public DbSet<IngredientStockMovements> IngredientStockMovements => Set<IngredientStockMovements>();
    public DbSet<Ingredients> Ingredients => Set<Ingredients>();
    public DbSet<MenuCategory> MenuCategory => Set<MenuCategory>();
    public DbSet<Menus> Menus => Set<Menus>();
    public DbSet<TableStatus> TableStatus => Set<TableStatus>();
    public DbSet<Units> Units => Set<Units>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Branches>(entity =>
        {
            entity.HasKey(e => e.BranchID);
            entity.HasIndex(e => e.IsActive).HasDatabaseName("idx_branches_active");
            entity.HasIndex(e => e.RestaurantID).HasDatabaseName("idx_branches_restaurant");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Location).HasMaxLength(500);
            entity.Property(e => e.ManagerName).HasMaxLength(100);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.OpeningHours).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Ignore(e => e.Employees);
            entity.Ignore(e => e.Restaurant);
        });

        modelBuilder.Entity<BusinessAuditLogs>(entity =>
        {
            entity.HasKey(e => e.BusinessAuditLogId);
            entity.HasIndex(e => e.CreatedAtUtc).HasDatabaseName("IX_BusinessAuditLogs_CreatedAtUtc");
            entity.HasIndex(e => new { e.EntityType, e.EntityId }).HasDatabaseName("IX_BusinessAuditLogs_Entity");
            entity.HasIndex(e => e.BranchId).HasDatabaseName("IX_BusinessAuditLogs_BranchId");
            entity.HasIndex(e => e.DishId).HasDatabaseName("IX_BusinessAuditLogs_DishId");
            entity.HasIndex(e => e.TableId).HasDatabaseName("IX_BusinessAuditLogs_TableId");
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

        modelBuilder.Entity<Categories>(entity =>
        {
            entity.HasKey(e => e.CategoryID);
            entity.HasIndex(e => e.IsActive).HasDatabaseName("idx_categories_active");
            entity.HasIndex(e => e.DisplayOrder).HasDatabaseName("idx_categories_display");
            entity.HasIndex(e => e.Name).HasDatabaseName("idx_categories_name");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<CategoryDish>(entity =>
        {
            entity.HasKey(e => e.CategoryDishID);
            entity.HasIndex(e => e.DishID).HasDatabaseName("idx_categorydish_dish");
            entity.HasIndex(e => e.DisplayOrder).HasDatabaseName("idx_categorydish_display");
            entity.HasIndex(e => e.MenuCategoryID).HasDatabaseName("idx_categorydish_menucategory");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
            entity.Property(e => e.IsAvailable).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.HasOne(e => e.Dish)
                .WithMany(d => d.CategoryDish)
                .HasForeignKey(e => e.DishID)
                .OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(e => e.MenuCategory)
                .WithMany(m => m.CategoryDish)
                .HasForeignKey(e => e.MenuCategoryID);
        });

        modelBuilder.Entity<DiningTables>(entity =>
        {
            entity.HasKey(e => e.TableID);
            entity.HasIndex(e => e.BranchID).HasDatabaseName("idx_tables_branch");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("idx_tables_active");
            entity.HasIndex(e => e.StatusID).HasDatabaseName("idx_tables_status");
            entity.HasIndex(e => new { e.BranchID, e.TableNumber })
                .IsUnique()
                .HasDatabaseName("UQ_DiningTables_Branch_TableNumber");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.QRCode).HasMaxLength(100);
            entity.Property(e => e.TableNumber).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Ignore(e => e.Orders);
            entity.HasOne(e => e.Branch)
                .WithMany(b => b.DiningTables)
                .HasForeignKey(e => e.BranchID);
            entity.HasOne(e => e.Status)
                .WithMany(s => s.DiningTables)
                .HasForeignKey(e => e.StatusID);
        });

        modelBuilder.Entity<DishIngredients>(entity =>
        {
            entity.HasKey(e => e.DishIngredientID);
            entity.HasIndex(e => new { e.DishID, e.IngredientID })
                .IsUnique()
                .HasDatabaseName("UQ_DishIngredients_Dish_Ingredient");
            entity.Property(e => e.QuantityPerDish).HasColumnType("decimal(18, 2)");
            entity.HasOne(e => e.Dish)
                .WithMany(d => d.DishIngredients)
                .HasForeignKey(e => e.DishID);
            entity.HasOne(e => e.Ingredient)
                .WithMany(i => i.DishIngredients)
                .HasForeignKey(e => e.IngredientID);
        });

        modelBuilder.Entity<Dishes>(entity =>
        {
            entity.HasKey(e => e.DishID);
            entity.HasIndex(e => e.CategoryID).HasDatabaseName("idx_dishes_category");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("idx_dishes_active");
            entity.HasIndex(e => e.Available).HasDatabaseName("idx_dishes_available");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Image).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Ignore(e => e.OrderItems);
            entity.HasOne(e => e.Category)
                .WithMany(c => c.Dishes)
                .HasForeignKey(e => e.CategoryID);
        });

        modelBuilder.Entity<Ingredients>(entity =>
        {
            entity.HasKey(e => e.IngredientID);
            entity.HasIndex(e => e.Name).HasDatabaseName("idx_ingredients_name");
            entity.Property(e => e.CurrentStock).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IssueMethod).HasMaxLength(10).IsUnicode(false).HasDefaultValue("FEFO");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.ReorderLevel).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Ignore(e => e.OrderItemIngredients);
        });

        modelBuilder.Entity<IngredientBatches>(entity =>
        {
            entity.HasKey(e => e.BatchID);
            entity.HasIndex(e => new { e.IngredientID, e.IsActive, e.ExpiryDate, e.ReceivedDate, e.BatchID })
                .HasDatabaseName("IX_IngredientBatches_Ingredient_Fefo");
            entity.HasIndex(e => e.ExpiryDate).HasDatabaseName("IX_IngredientBatches_ExpiryDate");
            entity.Property(e => e.BatchCode).HasMaxLength(100);
            entity.Property(e => e.QuantityInitial).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.QuantityRemaining).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.SupplierName).HasMaxLength(200);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2");
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasOne(e => e.Ingredient)
                .WithMany(i => i.IngredientBatches)
                .HasForeignKey(e => e.IngredientID);
        });

        modelBuilder.Entity<IngredientStockMovements>(entity =>
        {
            entity.HasKey(e => e.MovementID);
            entity.HasIndex(e => new { e.IngredientID, e.CreatedAt }).HasDatabaseName("IX_IngredientStockMovements_Ingredient_CreatedAt");
            entity.HasIndex(e => e.BatchID).HasDatabaseName("IX_IngredientStockMovements_BatchID");
            entity.Property(e => e.QuantityChange).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MovementType).HasMaxLength(50);
            entity.Property(e => e.ReferenceType).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.HasOne(e => e.Ingredient)
                .WithMany(i => i.IngredientStockMovements)
                .HasForeignKey(e => e.IngredientID)
                .OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(e => e.Batch)
                .WithMany(b => b.StockMovements)
                .HasForeignKey(e => e.BatchID)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<MenuCategory>(entity =>
        {
            entity.HasKey(e => e.MenuCategoryID);
            entity.HasIndex(e => new { e.MenuID, e.CategoryID })
                .IsUnique()
                .HasDatabaseName("UQ_MenuCategory_Menu_Category");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.HasOne(e => e.Category)
                .WithMany(c => c.MenuCategory)
                .HasForeignKey(e => e.CategoryID);
            entity.HasOne(e => e.Menu)
                .WithMany(m => m.MenuCategory)
                .HasForeignKey(e => e.MenuID);
        });

        modelBuilder.Entity<Menus>(entity =>
        {
            entity.HasKey(e => e.MenuID);
            entity.HasIndex(e => e.BranchID).HasDatabaseName("idx_menus_branch");
            entity.HasIndex(e => e.Date).HasDatabaseName("idx_menus_date");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MenuName).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.HasOne(e => e.Branch)
                .WithMany(b => b.Menus)
                .HasForeignKey(e => e.BranchID);
        });
        modelBuilder.Entity<TableStatus>(entity =>
        {
            entity.HasKey(e => e.StatusID);
            entity.Property(e => e.StatusCode).HasMaxLength(50);
            entity.Property(e => e.StatusName).HasMaxLength(100);
        });

        modelBuilder.Entity<Units>(entity =>
        {
            entity.HasKey(e => e.UnitID);
            entity.HasIndex(e => e.Name)
                .IsUnique()
                .HasDatabaseName("UX_Units_Name");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("IX_Units_IsActive");
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
        });
    }
}
