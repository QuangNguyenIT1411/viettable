using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Database.Entities;

namespace SelfRestaurant.Database;

public partial class RestaurantDbContext : DbContext
{
    public RestaurantDbContext()
    {
    }

    public RestaurantDbContext(DbContextOptions<RestaurantDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ActiveOrders> ActiveOrders { get; set; }

    public virtual DbSet<Bills> Bills { get; set; }

    public virtual DbSet<BranchRevenue> BranchRevenue { get; set; }

    public virtual DbSet<Branches> Branches { get; set; }

    public virtual DbSet<Categories> Categories { get; set; }

    public virtual DbSet<CategoryDish> CategoryDish { get; set; }

    public virtual DbSet<CustomerLoyalty> CustomerLoyalty { get; set; }

    public virtual DbSet<Customers> Customers { get; set; }

    public virtual DbSet<DiningTables> DiningTables { get; set; }

    public virtual DbSet<DishDetails> DishDetails { get; set; }

    public virtual DbSet<DishIngredients> DishIngredients { get; set; }

    public virtual DbSet<Dishes> Dishes { get; set; }

    public virtual DbSet<EmployeeRoles> EmployeeRoles { get; set; }

    public virtual DbSet<Employees> Employees { get; set; }

    public virtual DbSet<Ingredients> Ingredients { get; set; }

    public virtual DbSet<LoyaltyCards> LoyaltyCards { get; set; }

    public virtual DbSet<MenuCategory> MenuCategory { get; set; }

    public virtual DbSet<Menus> Menus { get; set; }

    public virtual DbSet<OrderItemIngredients> OrderItemIngredients { get; set; }

    public virtual DbSet<OrderItems> OrderItems { get; set; }

    public virtual DbSet<OrderStatus> OrderStatus { get; set; }

    public virtual DbSet<Orders> Orders { get; set; }

    public virtual DbSet<PasswordResetTokens> PasswordResetTokens { get; set; }

    public virtual DbSet<PaymentMethod> PaymentMethod { get; set; }

    public virtual DbSet<PaymentStatus> PaymentStatus { get; set; }

    public virtual DbSet<Payments> Payments { get; set; }

    public virtual DbSet<Reports> Reports { get; set; }

    public virtual DbSet<Restaurants> Restaurants { get; set; }

    public virtual DbSet<TableNumbers> TableNumbers { get; set; }

    public virtual DbSet<TableStatus> TableStatus { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            throw new InvalidOperationException(
                "RestaurantDbContext requires DbContextOptions. Register it via AddDbContext/UseSqlServer and pass options in.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActiveOrders>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("ActiveOrders");

            entity.Property(e => e.BranchName).HasMaxLength(200);
            entity.Property(e => e.CustomerName).HasMaxLength(100);
            entity.Property(e => e.OrderCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.OrderTime).HasColumnType("datetime");
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.StatusName).HasMaxLength(100);
        });

        modelBuilder.Entity<Bills>(entity =>
        {
            entity.HasKey(e => e.BillID).HasName("PK__Bills__11F2FC4AF4641263");

            entity.HasIndex(e => e.BillTime, "IX_Bills_BillTime");

            entity.HasIndex(e => e.OrderID, "IX_Bills_OrderID");

            entity.Property(e => e.BillCode).HasMaxLength(50);
            entity.Property(e => e.BillTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ChangeAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Discount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PaymentAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PaymentMethod).HasMaxLength(20);
            entity.Property(e => e.PointsDiscount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Subtotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Customer).WithMany(p => p.Bills)
                .HasForeignKey(d => d.CustomerID)
                .HasConstraintName("FK_Bills_Customers");

            entity.HasOne(d => d.Employee).WithMany(p => p.Bills)
                .HasForeignKey(d => d.EmployeeID)
                .HasConstraintName("FK_Bills_Employees");

            entity.HasOne(d => d.Order).WithMany(p => p.Bills)
                .HasForeignKey(d => d.OrderID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Bills_Orders");
        });

        modelBuilder.Entity<BranchRevenue>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("BranchRevenue");

            entity.Property(e => e.BranchName).HasMaxLength(200);
            entity.Property(e => e.TotalRevenue).HasColumnType("decimal(38, 2)");
        });

        modelBuilder.Entity<Branches>(entity =>
        {
            entity.HasKey(e => e.BranchID).HasName("PK__Branches__A1682FA550AA6189");

            entity.HasIndex(e => e.IsActive, "idx_branches_active");

            entity.HasIndex(e => e.RestaurantID, "idx_branches_restaurant");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Location).HasMaxLength(500);
            entity.Property(e => e.ManagerName).HasMaxLength(100);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.OpeningHours).HasMaxLength(100);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Restaurant).WithMany(p => p.Branches)
                .HasForeignKey(d => d.RestaurantID)
                .HasConstraintName("FK__Branches__Restau__2A164134");
        });

        modelBuilder.Entity<Categories>(entity =>
        {
            entity.HasKey(e => e.CategoryID).HasName("PK__Categori__19093A2BDDBDD672");

            entity.HasIndex(e => e.IsActive, "idx_categories_active");

            entity.HasIndex(e => e.DisplayOrder, "idx_categories_display");

            entity.HasIndex(e => e.Name, "idx_categories_name");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<CategoryDish>(entity =>
        {
            entity.HasKey(e => e.CategoryDishID).HasName("PK__Category__E66CC3A2CE27257C");

            entity.HasIndex(e => e.DishID, "idx_categorydish_dish");

            entity.HasIndex(e => e.DisplayOrder, "idx_categorydish_display");

            entity.HasIndex(e => e.MenuCategoryID, "idx_categorydish_menucategory");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
            entity.Property(e => e.IsAvailable).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Dish).WithMany(p => p.CategoryDish)
                .HasForeignKey(d => d.DishID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CategoryD__DishI__2B0A656D");

            entity.HasOne(d => d.MenuCategory).WithMany(p => p.CategoryDish)
                .HasForeignKey(d => d.MenuCategoryID)
                .HasConstraintName("FK__CategoryD__MenuC__2BFE89A6");
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

        modelBuilder.Entity<Customers>(entity =>
        {
            entity.HasKey(e => e.CustomerID).HasName("PK__Customer__A4AE64B847AF3499");

            entity.HasIndex(e => e.Username, "IX_Customer_Username")
                .IsUnique()
                .HasFilter("([Username] IS NOT NULL)");

            entity.HasIndex(e => e.Username, "UQ_Customers_Username").IsUnique();

            entity.HasIndex(e => e.IsActive, "idx_customers_active");

            entity.HasIndex(e => e.Email, "idx_customers_email");

            entity.HasIndex(e => e.PhoneNumber, "idx_customers_phone");

            entity.HasIndex(e => e.Username, "idx_customers_username");

            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Gender).HasMaxLength(10);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LoyaltyPoints).HasDefaultValue(0);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<DiningTables>(entity =>
        {
            entity.HasKey(e => e.TableID).HasName("PK__DiningTa__7D5F018E145FCE6C");

            entity.HasIndex(e => e.BranchID, "idx_diningtables_branch");

            entity.HasIndex(e => e.StatusID, "idx_diningtables_status");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.QRCode)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Branch).WithMany(p => p.DiningTables)
                .HasForeignKey(d => d.BranchID)
                .HasConstraintName("FK__DiningTab__Branc__2CF2ADDF");

            entity.HasOne(d => d.Status).WithMany(p => p.DiningTables)
                .HasForeignKey(d => d.StatusID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DiningTab__Statu__2DE6D218");
        });

        modelBuilder.Entity<DishDetails>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("DishDetails");

            entity.Property(e => e.CategoryName).HasMaxLength(200);
            entity.Property(e => e.DishName).HasMaxLength(200);
            entity.Property(e => e.Price).HasColumnType("decimal(15, 2)");
        });

        modelBuilder.Entity<DishIngredients>(entity =>
        {
            entity.HasKey(e => e.DishIngredientID).HasName("PK__DishIngr__DBEF3FC397F8EBC1");

            entity.Property(e => e.QuantityPerDish).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Dish).WithMany(p => p.DishIngredients)
                .HasForeignKey(d => d.DishID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DishIngredients_Dishes");

            entity.HasOne(d => d.Ingredient).WithMany(p => p.DishIngredients)
                .HasForeignKey(d => d.IngredientID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DishIngredients_Ingredients");
        });

        modelBuilder.Entity<Dishes>(entity =>
        {
            entity.HasKey(e => e.DishID).HasName("PK__Dishes__18834F70BE6EB032");

            entity.HasIndex(e => e.IsActive, "idx_dishes_active");

            entity.HasIndex(e => e.Available, "idx_dishes_available");

            entity.HasIndex(e => e.CategoryID, "idx_dishes_category");

            entity.HasIndex(e => e.Name, "idx_dishes_name");

            entity.Property(e => e.Available).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Image)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDailySpecial).HasDefaultValue(false);
            entity.Property(e => e.IsVegetarian).HasDefaultValue(false);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Price).HasColumnType("decimal(15, 2)");
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Category).WithMany(p => p.Dishes)
                .HasForeignKey(d => d.CategoryID)
                .HasConstraintName("FK__Dishes__Category__2EDAF651");
        });

        modelBuilder.Entity<EmployeeRoles>(entity =>
        {
            entity.HasKey(e => e.RoleID).HasName("PK__Employee__8AFACE3A2A11B334");

            entity.HasIndex(e => e.RoleCode, "UQ__Employee__D62CB59C6E6A8DBA").IsUnique();

            entity.Property(e => e.RoleCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RoleName).HasMaxLength(100);
        });

        modelBuilder.Entity<Employees>(entity =>
        {
            entity.HasKey(e => e.EmployeeID).HasName("PK__Employee__7AD04FF149590C00");

            entity.HasIndex(e => e.Username, "UQ__Employee__536C85E4C7F7CB66").IsUnique();

            entity.HasIndex(e => e.IsActive, "idx_employees_active");

            entity.HasIndex(e => e.BranchID, "idx_employees_branch");

            entity.HasIndex(e => e.RoleID, "idx_employees_role");

            entity.HasIndex(e => e.Username, "idx_employees_username");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Salary).HasColumnType("decimal(15, 2)");
            entity.Property(e => e.Shift).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.Branch).WithMany(p => p.Employees)
                .HasForeignKey(d => d.BranchID)
                .HasConstraintName("FK__Employees__Branc__31B762FC");

            entity.HasOne(d => d.Role).WithMany(p => p.Employees)
                .HasForeignKey(d => d.RoleID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Employees__RoleI__32AB8735");
        });

        modelBuilder.Entity<Ingredients>(entity =>
        {
            entity.HasKey(e => e.IngredientID).HasName("PK__Ingredie__BEAEB27A6761FDC2");

            entity.Property(e => e.CurrentStock).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.ReorderLevel).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Unit).HasMaxLength(50);
        });

        modelBuilder.Entity<LoyaltyCards>(entity =>
        {
            entity.HasKey(e => e.CardID).HasName("PK__LoyaltyC__55FECD8E5F04BC95");

            entity.HasIndex(e => e.IsActive, "idx_loyaltycards_active");

            entity.HasIndex(e => e.CustomerID, "idx_loyaltycards_customer");

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Points).HasDefaultValue(0);

            entity.HasOne(d => d.Customer).WithMany(p => p.LoyaltyCards)
                .HasForeignKey(d => d.CustomerID)
                .HasConstraintName("FK__LoyaltyCa__Custo__339FAB6E");
        });

        modelBuilder.Entity<MenuCategory>(entity =>
        {
            entity.HasKey(e => e.MenuCategoryID).HasName("PK__MenuCate__5AF617DB0A1C265D");

            entity.HasIndex(e => e.CategoryID, "idx_menucategory_category");

            entity.HasIndex(e => e.MenuID, "idx_menucategory_menu");

            entity.HasIndex(e => new { e.MenuID, e.CategoryID }, "unique_menu_category").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Category).WithMany(p => p.MenuCategory)
                .HasForeignKey(d => d.CategoryID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MenuCateg__Categ__3493CFA7");

            entity.HasOne(d => d.Menu).WithMany(p => p.MenuCategory)
                .HasForeignKey(d => d.MenuID)
                .HasConstraintName("FK__MenuCateg__MenuI__3587F3E0");
        });

        modelBuilder.Entity<Menus>(entity =>
        {
            entity.HasKey(e => e.MenuID).HasName("PK__Menus__C99ED25034EB70B2");

            entity.HasIndex(e => e.IsActive, "idx_menus_active");

            entity.HasIndex(e => e.BranchID, "idx_menus_branch");

            entity.HasIndex(e => e.Date, "idx_menus_date");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MenuName).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Branch).WithMany(p => p.Menus)
                .HasForeignKey(d => d.BranchID)
                .HasConstraintName("FK__Menus__BranchID__367C1819");
        });

        modelBuilder.Entity<OrderItemIngredients>(entity =>
        {
            entity.HasKey(e => e.OrderItemIngredientID).HasName("PK__OrderIte__6DC447D5F3D8C5FF");

            entity.HasIndex(e => e.OrderItemID, "IX_OrderItemIngredients_OrderItemID");

            entity.HasIndex(e => new { e.OrderItemID, e.IngredientID }, "UQ_OrderItemIngredients_OrderItem_Ingredient").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Ingredient).WithMany(p => p.OrderItemIngredients)
                .HasForeignKey(d => d.IngredientID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderItemIngredients_Ingredients");

            entity.HasOne(d => d.OrderItem).WithMany(p => p.OrderItemIngredients)
                .HasForeignKey(d => d.OrderItemID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderItemIngredients_OrderItems");
        });

        modelBuilder.Entity<OrderItems>(entity =>
        {
            entity.HasKey(e => e.ItemID).HasName("PK__OrderIte__727E83EBBAFCB759");

            entity.HasIndex(e => e.DishID, "idx_orderitems_dish");

            entity.HasIndex(e => e.OrderID, "idx_orderitems_order");

            entity.Property(e => e.LineTotal).HasColumnType("decimal(15, 2)");
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(15, 2)");

            entity.HasOne(d => d.Dish).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.DishID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OrderItem__DishI__395884C4");
        });

        modelBuilder.Entity<OrderStatus>(entity =>
        {
            entity.HasKey(e => e.StatusID).HasName("PK__OrderSta__C8EE20430CC75471");

            entity.HasIndex(e => e.StatusCode, "UQ__OrderSta__6A7B44FCC4D0D8FB").IsUnique();

            entity.Property(e => e.StatusCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.StatusName).HasMaxLength(100);
        });

        modelBuilder.Entity<Orders>(entity =>
        {
            entity.HasKey(e => e.OrderID).HasName("PK__Orders__C3905BAF451966E9");

            entity.HasIndex(e => e.OrderCode, "UQ__Orders__999B5229B9E12DAC").IsUnique();

            entity.HasIndex(e => e.OrderCode, "idx_orders_code");

            entity.HasIndex(e => e.CustomerID, "idx_orders_customer");

            entity.HasIndex(e => e.StatusID, "idx_orders_status");

            entity.HasIndex(e => e.TableID, "idx_orders_table");

            entity.HasIndex(e => e.OrderTime, "idx_orders_time");

            entity.Property(e => e.CompletedTime).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.OrderCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.OrderTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Cashier).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CashierID)
                .HasConstraintName("FK_Orders_Cashier");

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerID)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK__Orders__Customer__3A4CA8FD");

            entity.HasOne(d => d.Status).WithMany(p => p.Orders)
                .HasForeignKey(d => d.StatusID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Orders__StatusID__3B40CD36");

            entity.HasOne(d => d.Table).WithMany(p => p.Orders)
                .HasForeignKey(d => d.TableID)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK__Orders__TableID__3C34F16F");
        });

        modelBuilder.Entity<PasswordResetTokens>(entity =>
        {
            entity.HasKey(e => e.TokenID).HasName("PK__Password__658FEE8A3196D933");

            entity.HasIndex(e => e.CustomerID, "idx_customer");

            entity.HasIndex(e => e.Token, "idx_token");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ExpiryDate).HasColumnType("datetime");
            entity.Property(e => e.Token)
                .HasMaxLength(255)
                .IsUnicode(false);

            entity.HasOne(d => d.Customer).WithMany(p => p.PasswordResetTokens)
                .HasForeignKey(d => d.CustomerID)
                .HasConstraintName("FK__PasswordR__Custo__3E1D39E1");
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.HasKey(e => e.MethodID).HasName("PK__PaymentM__FC681FB141EDB576");

            entity.HasIndex(e => e.MethodCode, "UQ__PaymentM__11E9210D94A7498F").IsUnique();

            entity.Property(e => e.MethodCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.MethodName).HasMaxLength(100);
        });

        modelBuilder.Entity<PaymentStatus>(entity =>
        {
            entity.HasKey(e => e.StatusID).HasName("PK__PaymentS__C8EE2043428EA6B8");

            entity.HasIndex(e => e.StatusCode, "UQ__PaymentS__6A7B44FCDCB5DB9F").IsUnique();

            entity.Property(e => e.StatusCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.StatusName).HasMaxLength(100);
        });

        modelBuilder.Entity<Payments>(entity =>
        {
            entity.HasKey(e => e.PaymentID).HasName("PK__Payments__9B556A589D155A97");

            entity.HasIndex(e => e.CustomerID, "idx_payments_customer");

            entity.HasIndex(e => e.Date, "idx_payments_date");

            entity.HasIndex(e => e.OrderID, "idx_payments_order");

            entity.HasIndex(e => e.StatusID, "idx_payments_status");

            entity.Property(e => e.Amount).HasColumnType("decimal(15, 2)");
            entity.Property(e => e.Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Customer).WithMany(p => p.Payments)
                .HasForeignKey(d => d.CustomerID)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK__Payments__Custom__3F115E1A");

            entity.HasOne(d => d.Method).WithMany(p => p.Payments)
                .HasForeignKey(d => d.MethodID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Payments__Method__40058253");

            entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                .HasForeignKey(d => d.OrderID)
                .HasConstraintName("FK__Payments__OrderI__40F9A68C");

            entity.HasOne(d => d.Status).WithMany(p => p.Payments)
                .HasForeignKey(d => d.StatusID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Payments__Status__41EDCAC5");
        });

        modelBuilder.Entity<Reports>(entity =>
        {
            entity.HasKey(e => e.ReportID).HasName("PK__Reports__D5BD48E53DF151EA");

            entity.HasIndex(e => e.GeneratedDate, "idx_reports_date");

            entity.HasIndex(e => e.ReportType, "idx_reports_type");

            entity.Property(e => e.FilePath)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.GeneratedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ReportType).HasMaxLength(100);
        });

        modelBuilder.Entity<Restaurants>(entity =>
        {
            entity.HasKey(e => e.RestaurantID).HasName("PK__Restaura__87454CB5BC329545");

            entity.HasIndex(e => e.IsActive, "idx_restaurants_active");

            entity.HasIndex(e => e.Name, "idx_restaurants_name");

            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<TableNumbers>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("TableNumbers");

            entity.Property(e => e.BranchName).HasMaxLength(200);
            entity.Property(e => e.QRCode)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.StatusName).HasMaxLength(100);
        });

        modelBuilder.Entity<TableStatus>(entity =>
        {
            entity.HasKey(e => e.StatusID).HasName("PK__TableSta__C8EE2043989A66BB");

            entity.HasIndex(e => e.StatusCode, "UQ__TableSta__6A7B44FC569A8521").IsUnique();

            entity.Property(e => e.StatusCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.StatusName).HasMaxLength(100);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
