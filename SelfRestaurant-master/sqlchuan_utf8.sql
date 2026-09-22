USE [master]
GO
/****** Object:  Database [RESTAURANT]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE DATABASE [RESTAURANT]
 CONTAINMENT = NONE
 ON  PRIMARY 
( NAME = N'RESTAURANT', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\RESTAURANT.mdf' , SIZE = 73728KB , MAXSIZE = UNLIMITED, FILEGROWTH = 65536KB )
 LOG ON 
( NAME = N'RESTAURANT_log', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\RESTAURANT_log.ldf' , SIZE = 8192KB , MAXSIZE = 2048GB , FILEGROWTH = 65536KB )
 WITH CATALOG_COLLATION = DATABASE_DEFAULT, LEDGER = OFF
GO
ALTER DATABASE [RESTAURANT] SET COMPATIBILITY_LEVEL = 160
GO
IF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))
begin
EXEC [RESTAURANT].[dbo].[sp_fulltext_database] @action = 'enable'
end
GO
ALTER DATABASE [RESTAURANT] SET ANSI_NULL_DEFAULT OFF 
GO
ALTER DATABASE [RESTAURANT] SET ANSI_NULLS OFF 
GO
ALTER DATABASE [RESTAURANT] SET ANSI_PADDING OFF 
GO
ALTER DATABASE [RESTAURANT] SET ANSI_WARNINGS OFF 
GO
ALTER DATABASE [RESTAURANT] SET ARITHABORT OFF 
GO
ALTER DATABASE [RESTAURANT] SET AUTO_CLOSE ON 
GO
ALTER DATABASE [RESTAURANT] SET AUTO_SHRINK OFF 
GO
ALTER DATABASE [RESTAURANT] SET AUTO_UPDATE_STATISTICS ON 
GO
ALTER DATABASE [RESTAURANT] SET CURSOR_CLOSE_ON_COMMIT OFF 
GO
ALTER DATABASE [RESTAURANT] SET CURSOR_DEFAULT  GLOBAL 
GO
ALTER DATABASE [RESTAURANT] SET CONCAT_NULL_YIELDS_NULL OFF 
GO
ALTER DATABASE [RESTAURANT] SET NUMERIC_ROUNDABORT OFF 
GO
ALTER DATABASE [RESTAURANT] SET QUOTED_IDENTIFIER OFF 
GO
ALTER DATABASE [RESTAURANT] SET RECURSIVE_TRIGGERS OFF 
GO
ALTER DATABASE [RESTAURANT] SET  ENABLE_BROKER 
GO
ALTER DATABASE [RESTAURANT] SET AUTO_UPDATE_STATISTICS_ASYNC OFF 
GO
ALTER DATABASE [RESTAURANT] SET DATE_CORRELATION_OPTIMIZATION OFF 
GO
ALTER DATABASE [RESTAURANT] SET TRUSTWORTHY OFF 
GO
ALTER DATABASE [RESTAURANT] SET ALLOW_SNAPSHOT_ISOLATION OFF 
GO
ALTER DATABASE [RESTAURANT] SET PARAMETERIZATION SIMPLE 
GO
ALTER DATABASE [RESTAURANT] SET READ_COMMITTED_SNAPSHOT OFF 
GO
ALTER DATABASE [RESTAURANT] SET HONOR_BROKER_PRIORITY OFF 
GO
ALTER DATABASE [RESTAURANT] SET RECOVERY SIMPLE 
GO
ALTER DATABASE [RESTAURANT] SET  MULTI_USER 
GO
ALTER DATABASE [RESTAURANT] SET PAGE_VERIFY CHECKSUM  
GO
ALTER DATABASE [RESTAURANT] SET DB_CHAINING OFF 
GO
ALTER DATABASE [RESTAURANT] SET FILESTREAM( NON_TRANSACTED_ACCESS = OFF ) 
GO
ALTER DATABASE [RESTAURANT] SET TARGET_RECOVERY_TIME = 60 SECONDS 
GO
ALTER DATABASE [RESTAURANT] SET DELAYED_DURABILITY = DISABLED 
GO
ALTER DATABASE [RESTAURANT] SET ACCELERATED_DATABASE_RECOVERY = OFF  
GO
ALTER DATABASE [RESTAURANT] SET QUERY_STORE = ON
GO
ALTER DATABASE [RESTAURANT] SET QUERY_STORE (OPERATION_MODE = READ_WRITE, CLEANUP_POLICY = (STALE_QUERY_THRESHOLD_DAYS = 30), DATA_FLUSH_INTERVAL_SECONDS = 900, INTERVAL_LENGTH_MINUTES = 60, MAX_STORAGE_SIZE_MB = 1000, QUERY_CAPTURE_MODE = AUTO, SIZE_BASED_CLEANUP_MODE = AUTO, MAX_PLANS_PER_QUERY = 200, WAIT_STATS_CAPTURE_MODE = ON)
GO
USE [RESTAURANT]
GO
/****** Object:  Table [dbo].[Orders]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Orders](
	[OrderID] [int] IDENTITY(1,1) NOT NULL,
	[OrderCode] [varchar](50) NULL,
	[OrderTime] [datetime] NOT NULL,
	[CompletedTime] [datetime] NULL,
	[Note] [nvarchar](max) NULL,
	[IsActive] [bit] NULL,
	[TableID] [int] NULL,
	[CustomerID] [int] NULL,
	[StatusID] [int] NOT NULL,
	[CashierID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[OrderID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[OrderStatus]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[OrderStatus](
	[StatusID] [int] IDENTITY(1,1) NOT NULL,
	[StatusCode] [varchar](50) NOT NULL,
	[StatusName] [nvarchar](100) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[StatusID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Branches]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Branches](
	[BranchID] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](200) NOT NULL,
	[Location] [nvarchar](500) NULL,
	[ManagerName] [nvarchar](100) NULL,
	[Phone] [varchar](20) NULL,
	[Email] [varchar](100) NULL,
	[OpeningHours] [nvarchar](100) NULL,
	[IsActive] [bit] NULL,
	[CreatedAt] [datetime] NULL,
	[UpdatedAt] [datetime] NULL,
	[RestaurantID] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[BranchID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[DiningTables]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DiningTables](
	[TableID] [int] IDENTITY(1,1) NOT NULL,
	[NumberOfSeats] [int] NOT NULL,
	[CurrentOrderID] [int] NULL,
	[QRCode] [varchar](200) NULL,
	[IsActive] [bit] NULL,
	[CreatedAt] [datetime] NULL,
	[UpdatedAt] [datetime] NULL,
	[BranchID] [int] NOT NULL,
	[StatusID] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[TableID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Customers]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Customers](
	[CustomerID] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[PhoneNumber] [varchar](20) NOT NULL,
	[Email] [varchar](100) NULL,
	[Gender] [nvarchar](10) NULL,
	[DateOfBirth] [date] NULL,
	[Address] [nvarchar](500) NULL,
	[LoyaltyPoints] [int] NULL,
	[CreatedAt] [datetime] NULL,
	[UpdatedAt] [datetime] NULL,
	[IsActive] [bit] NULL,
	[Username] [varchar](50) NOT NULL,
	[Password] [varchar](255) NOT NULL,
	[CreditPoints] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[CustomerID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  View [dbo].[ActiveOrders]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [dbo].[ActiveOrders] AS
SELECT 
    o.OrderID,
    o.OrderCode,
    o.OrderTime,
    c.Name AS CustomerName,
    c.PhoneNumber,
    dt.NumberOfSeats AS TableSeats,
    os.StatusName,
    b.Name AS BranchName
FROM Orders o
LEFT JOIN Customers c ON o.CustomerID = c.CustomerID
LEFT JOIN DiningTables dt ON o.TableID = dt.TableID
LEFT JOIN OrderStatus os ON o.StatusID = os.StatusID
LEFT JOIN Branches b ON dt.BranchID = b.BranchID
WHERE o.IsActive = 1;
GO
/****** Object:  Table [dbo].[Categories]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Categories](
	[CategoryID] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](200) NOT NULL,
	[Description] [nvarchar](max) NULL,
	[DisplayOrder] [int] NULL,
	[IsActive] [bit] NULL,
	[CreatedAt] [datetime] NULL,
	[UpdatedAt] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[CategoryID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Dishes]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Dishes](
	[DishID] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](200) NOT NULL,
	[Price] [decimal](15, 2) NOT NULL,
	[Available] [bit] NULL,
	[Image] [varchar](500) NULL,
	[Description] [nvarchar](max) NULL,
	[Unit] [nvarchar](50) NULL,
	[IsVegetarian] [bit] NULL,
	[IsDailySpecial] [bit] NULL,
	[IsActive] [bit] NULL,
	[CreatedAt] [datetime] NULL,
	[UpdatedAt] [datetime] NULL,
	[CategoryID] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[DishID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  View [dbo].[DishDetails]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- View: Thông tin món ăn và danh mục
CREATE VIEW [dbo].[DishDetails] AS
SELECT 
    d.DishID,
    d.Name AS DishName,
    d.Price,
    d.Available,
    d.IsVegetarian,
    d.IsDailySpecial,
    c.Name AS CategoryName,
    c.CategoryID
FROM Dishes d
INNER JOIN Categories c ON d.CategoryID = c.CategoryID
WHERE d.IsActive = 1 AND d.Available = 1;
GO
/****** Object:  Table [dbo].[Bills]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Bills](
	[BillID] [int] IDENTITY(1,1) NOT NULL,
	[OrderID] [int] NOT NULL,
	[BillCode] [nvarchar](50) NOT NULL,
	[BillTime] [datetime] NOT NULL,
	[Subtotal] [decimal](18, 2) NOT NULL,
	[Discount] [decimal](18, 2) NOT NULL,
	[PointsDiscount] [decimal](18, 2) NOT NULL,
	[PointsUsed] [int] NULL,
	[TotalAmount] [decimal](18, 2) NOT NULL,
	[PaymentMethod] [nvarchar](20) NOT NULL,
	[PaymentAmount] [decimal](18, 2) NULL,
	[ChangeAmount] [decimal](18, 2) NULL,
	[EmployeeID] [int] NULL,
	[CustomerID] [int] NULL,
	[IsActive] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[BillID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  View [dbo].[BranchRevenue]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- View: Thống kê doanh thu theo chi nhánh
CREATE VIEW [dbo].[BranchRevenue] AS
SELECT 
    b.BranchID,
    b.Name AS BranchName,
    COUNT(DISTINCT o.OrderID) AS TotalOrders,
    SUM(bill.TotalAmount - bill.Discount) AS TotalRevenue,
    CAST(o.OrderTime AS DATE) AS OrderDate
FROM Branches b
LEFT JOIN DiningTables dt ON b.BranchID = dt.BranchID
LEFT JOIN Orders o ON dt.TableID = o.TableID
LEFT JOIN Bills bill ON o.OrderID = bill.OrderID
WHERE o.IsActive = 1
GROUP BY b.BranchID, b.Name, CAST(o.OrderTime AS DATE);
GO
/****** Object:  Table [dbo].[LoyaltyCards]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[LoyaltyCards](
	[CardID] [int] IDENTITY(1,1) NOT NULL,
	[Points] [int] NULL,
	[IssueDate] [date] NOT NULL,
	[ExpiryDate] [date] NULL,
	[IsActive] [bit] NULL,
	[CustomerID] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[CardID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  View [dbo].[CustomerLoyalty]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- View: Thông tin khách hàng và điểm thưởng
CREATE VIEW [dbo].[CustomerLoyalty] AS
SELECT 
    c.CustomerID,
    c.Name,
    c.PhoneNumber,
    c.Email,
    c.LoyaltyPoints,
    lc.CardID,
    lc.Points AS CardPoints,
    lc.IssueDate,
    lc.ExpiryDate
FROM Customers c
LEFT JOIN LoyaltyCards lc ON c.CustomerID = lc.CustomerID
WHERE c.IsActive = 1;
GO
/****** Object:  Table [dbo].[TableStatus]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TableStatus](
	[StatusID] [int] IDENTITY(1,1) NOT NULL,
	[StatusCode] [varchar](50) NOT NULL,
	[StatusName] [nvarchar](100) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[StatusID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  View [dbo].[TableNumbers]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- Tạo view mới
CREATE VIEW [dbo].[TableNumbers] AS
SELECT 
    dt.TableID,
    dt.BranchID,
    b.Name AS BranchName,
    ROW_NUMBER() OVER (PARTITION BY dt.BranchID ORDER BY dt.TableID) AS TableNumber,
    dt.NumberOfSeats,
    dt.QRCode,
    ts.StatusName,
    dt.CurrentOrderID,
    dt.IsActive
FROM DiningTables dt
INNER JOIN Branches b ON dt.BranchID = b.BranchID
INNER JOIN TableStatus ts ON dt.StatusID = ts.StatusID;
GO
/****** Object:  Table [dbo].[CategoryDish]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CategoryDish](
	[CategoryDishID] [int] IDENTITY(1,1) NOT NULL,
	[DisplayOrder] [int] NULL,
	[IsAvailable] [bit] NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedAt] [datetime] NULL,
	[UpdatedAt] [datetime] NULL,
	[MenuCategoryID] [int] NOT NULL,
	[DishID] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[CategoryDishID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[DishIngredients]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DishIngredients](
	[DishIngredientID] [int] IDENTITY(1,1) NOT NULL,
	[DishID] [int] NOT NULL,
	[IngredientID] [int] NOT NULL,
	[QuantityPerDish] [decimal](18, 2) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[DishIngredientID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[EmployeeRoles]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EmployeeRoles](
	[RoleID] [int] IDENTITY(1,1) NOT NULL,
	[RoleCode] [varchar](50) NOT NULL,
	[RoleName] [nvarchar](100) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[RoleID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Employees]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Employees](
	[EmployeeID] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[Username] [varchar](50) NOT NULL,
	[Password] [varchar](255) NOT NULL,
	[Phone] [varchar](20) NULL,
	[Email] [varchar](100) NULL,
	[Salary] [decimal](15, 2) NULL,
	[Shift] [nvarchar](50) NULL,
	[IsActive] [bit] NULL,
	[CreatedAt] [datetime] NULL,
	[UpdatedAt] [datetime] NULL,
	[BranchID] [int] NOT NULL,
	[RoleID] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[EmployeeID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Ingredients]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Ingredients](
	[IngredientID] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](200) NOT NULL,
	[Unit] [nvarchar](50) NOT NULL,
	[CurrentStock] [decimal](18, 2) NOT NULL,
	[ReorderLevel] [decimal](18, 2) NOT NULL,
	[IsActive] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IngredientID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MenuCategory]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MenuCategory](
	[MenuCategoryID] [int] IDENTITY(1,1) NOT NULL,
	[CreatedAt] [datetime] NULL,
	[UpdatedAt] [datetime] NULL,
	[IsActive] [bit] NULL,
	[MenuID] [int] NOT NULL,
	[CategoryID] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[MenuCategoryID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Menus]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Menus](
	[MenuID] [int] IDENTITY(1,1) NOT NULL,
	[MenuName] [nvarchar](200) NOT NULL,
	[Date] [date] NULL,
	[IsActive] [bit] NULL,
	[CreatedAt] [datetime] NULL,
	[UpdatedAt] [datetime] NULL,
	[BranchID] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[MenuID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[OrderItemIngredients]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[OrderItemIngredients](
	[OrderItemIngredientID] [int] IDENTITY(1,1) NOT NULL,
	[OrderItemID] [int] NOT NULL,
	[IngredientID] [int] NOT NULL,
	[Quantity] [decimal](18, 3) NOT NULL,
	[IsRemoved] [bit] NOT NULL,
	[CreatedAt] [datetime] NULL,
	[UpdatedAt] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[OrderItemIngredientID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[OrderItems]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[OrderItems](
	[ItemID] [int] IDENTITY(1,1) NOT NULL,
	[Quantity] [int] NOT NULL,
	[UnitPrice] [decimal](15, 2) NOT NULL,
	[LineTotal] [decimal](15, 2) NOT NULL,
	[Note] [nvarchar](max) NULL,
	[OrderID] [int] NOT NULL,
	[DishID] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[ItemID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PasswordResetTokens]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PasswordResetTokens](
	[TokenID] [int] IDENTITY(1,1) NOT NULL,
	[CustomerID] [int] NOT NULL,
	[Token] [varchar](255) NOT NULL,
	[ExpiryDate] [datetime] NOT NULL,
	[IsUsed] [bit] NOT NULL,
	[CreatedAt] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[TokenID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PaymentMethod]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PaymentMethod](
	[MethodID] [int] IDENTITY(1,1) NOT NULL,
	[MethodCode] [varchar](50) NOT NULL,
	[MethodName] [nvarchar](100) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[MethodID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Payments]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Payments](
	[PaymentID] [int] IDENTITY(1,1) NOT NULL,
	[Amount] [decimal](15, 2) NOT NULL,
	[Date] [datetime] NOT NULL,
	[OrderID] [int] NOT NULL,
	[CustomerID] [int] NULL,
	[MethodID] [int] NOT NULL,
	[StatusID] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[PaymentID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PaymentStatus]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PaymentStatus](
	[StatusID] [int] IDENTITY(1,1) NOT NULL,
	[StatusCode] [varchar](50) NOT NULL,
	[StatusName] [nvarchar](100) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[StatusID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Reports]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Reports](
	[ReportID] [int] IDENTITY(1,1) NOT NULL,
	[ReportType] [nvarchar](100) NOT NULL,
	[GeneratedDate] [datetime] NOT NULL,
	[Content] [nvarchar](max) NULL,
	[FilePath] [varchar](500) NULL,
PRIMARY KEY CLUSTERED 
(
	[ReportID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Restaurants]    Script Date: 03/12/2025 2:23:11 SA ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Restaurants](
	[RestaurantID] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](200) NOT NULL,
	[Address] [nvarchar](500) NULL,
	[Description] [nvarchar](max) NULL,
	[Phone] [varchar](20) NULL,
	[Email] [varchar](100) NULL,
	[IsActive] [bit] NULL,
	[CreatedAt] [datetime] NULL,
	[UpdatedAt] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[RestaurantID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
SET IDENTITY_INSERT [dbo].[Branches] ON 

INSERT [dbo].[Branches] ([BranchID], [Name], [Location], [ManagerName], [Phone], [Email], [OpeningHours], [IsActive], [CreatedAt], [UpdatedAt], [RestaurantID]) VALUES (1, N'Chi nhánh Quận 1', N'123 Nguyễn Huệ, Quận 1, TP.HCM', N'Nguyễn Văn A', N'0901234567', N'quan1@selfrestaurant.com', N'7:00 - 22:00', 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 1)
INSERT [dbo].[Branches] ([BranchID], [Name], [Location], [ManagerName], [Phone], [Email], [OpeningHours], [IsActive], [CreatedAt], [UpdatedAt], [RestaurantID]) VALUES (2, N'Chi nhánh Gò Vấp', N'456 Quang Trung, Gò Vấp, TP.HCM', N'Trần Thị B', N'0907654321', N'govap@selfrestaurant.com', N'7:00 - 22:00', 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 1)
INSERT [dbo].[Branches] ([BranchID], [Name], [Location], [ManagerName], [Phone], [Email], [OpeningHours], [IsActive], [CreatedAt], [UpdatedAt], [RestaurantID]) VALUES (3, N'Chi nhánh Bình Thạnh', N'789 Điện Biên Phủ, Bình Thạnh, TP.HCM', N'Lê Văn C', N'0909876543', N'binhthanh@selfrestaurant.com', N'7:00 - 22:00', 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 1)
SET IDENTITY_INSERT [dbo].[Branches] OFF
GO
SET IDENTITY_INSERT [dbo].[Categories] ON 

INSERT [dbo].[Categories] ([CategoryID], [Name], [Description], [DisplayOrder], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (1, N'Món chính', N'Các món ăn chính', 1, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime))
INSERT [dbo].[Categories] ([CategoryID], [Name], [Description], [DisplayOrder], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (2, N'Món phụ', N'Các món ăn phụ', 2, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime))
INSERT [dbo].[Categories] ([CategoryID], [Name], [Description], [DisplayOrder], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (3, N'Tráng miệng', N'Các món tráng miệng', 3, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime))
INSERT [dbo].[Categories] ([CategoryID], [Name], [Description], [DisplayOrder], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (4, N'Đồ uống', N'Các loại đồ uống', 4, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime))
SET IDENTITY_INSERT [dbo].[Categories] OFF
GO
SET IDENTITY_INSERT [dbo].[CategoryDish] ON 

INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (1, 1, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 1, 1)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (2, 2, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T00:09:31.713' AS DateTime), 1, 2)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (3, 3, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 1, 3)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (4, 4, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T02:03:03.197' AS DateTime), 1, 7)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (5, 1, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 2, 8)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (6, 2, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 2, 9)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (7, 3, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 2, 10)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (8, 1, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 3, 11)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (9, 2, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 3, 12)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (10, 3, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 3, 13)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (11, 1, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 4, 14)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (12, 2, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 4, 15)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (13, 3, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 4, 16)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (14, 4, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 4, 17)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (15, 1, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 5, 3)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (16, 2, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-02T23:32:50.873' AS DateTime), 5, 4)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (17, 3, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 5, 6)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (18, 1, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 6, 8)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (19, 2, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 6, 9)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (20, 1, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 7, 14)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (21, 2, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 7, 15)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (22, 3, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 7, 16)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (23, 1, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 8, 5)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (24, 2, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 8, 1)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (25, 3, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T02:03:03.197' AS DateTime), 8, 7)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (26, 1, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 9, 11)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (27, 2, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 9, 13)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (28, 1, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 10, 14)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (29, 2, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 10, 16)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (30, 3, 1, NULL, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 10, 17)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (31, 1, 1, NULL, CAST(N'2025-11-19T14:22:32.900' AS DateTime), CAST(N'2025-12-02T23:32:50.873' AS DateTime), 11, 4)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (32, 2, 1, NULL, CAST(N'2025-11-19T14:22:32.903' AS DateTime), CAST(N'2025-12-03T00:09:31.713' AS DateTime), 11, 2)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (33, 3, 1, NULL, CAST(N'2025-11-19T14:22:32.903' AS DateTime), CAST(N'2025-12-03T02:03:03.197' AS DateTime), 11, 7)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (34, 4, 1, NULL, CAST(N'2025-11-19T14:22:32.903' AS DateTime), CAST(N'2025-11-19T14:22:32.903' AS DateTime), 11, 3)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (35, 5, 1, NULL, CAST(N'2025-11-19T14:22:32.907' AS DateTime), CAST(N'2025-11-19T14:22:32.907' AS DateTime), 11, 5)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (36, 6, 1, NULL, CAST(N'2025-11-19T14:22:32.907' AS DateTime), CAST(N'2025-11-19T14:22:32.907' AS DateTime), 11, 6)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (37, 7, 1, NULL, CAST(N'2025-11-19T14:22:32.907' AS DateTime), CAST(N'2025-11-19T14:22:32.907' AS DateTime), 11, 1)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (38, 1, 1, NULL, CAST(N'2025-11-19T14:22:32.923' AS DateTime), CAST(N'2025-11-19T14:22:32.923' AS DateTime), 12, 9)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (39, 2, 1, NULL, CAST(N'2025-11-19T14:22:32.923' AS DateTime), CAST(N'2025-11-19T14:22:32.923' AS DateTime), 12, 8)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (40, 3, 1, NULL, CAST(N'2025-11-19T14:22:32.923' AS DateTime), CAST(N'2025-11-19T14:22:32.923' AS DateTime), 12, 10)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (41, 1, 1, NULL, CAST(N'2025-11-19T14:22:32.930' AS DateTime), CAST(N'2025-11-19T14:22:32.930' AS DateTime), 13, 12)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (42, 2, 1, NULL, CAST(N'2025-11-19T14:22:32.930' AS DateTime), CAST(N'2025-11-19T14:22:32.930' AS DateTime), 13, 11)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (43, 3, 1, NULL, CAST(N'2025-11-19T14:22:32.930' AS DateTime), CAST(N'2025-11-19T14:22:32.930' AS DateTime), 13, 13)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (44, 1, 1, NULL, CAST(N'2025-11-19T14:22:32.937' AS DateTime), CAST(N'2025-11-19T14:22:32.937' AS DateTime), 14, 16)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (45, 2, 1, NULL, CAST(N'2025-11-19T14:22:32.937' AS DateTime), CAST(N'2025-11-19T14:22:32.937' AS DateTime), 14, 14)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (46, 3, 1, NULL, CAST(N'2025-11-19T14:22:32.937' AS DateTime), CAST(N'2025-11-19T14:22:32.937' AS DateTime), 14, 17)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (47, 4, 1, NULL, CAST(N'2025-11-19T14:22:32.940' AS DateTime), CAST(N'2025-11-19T14:22:32.940' AS DateTime), 14, 15)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (48, 1, 1, NULL, CAST(N'2025-11-19T22:56:21.000' AS DateTime), CAST(N'2025-12-02T23:32:50.873' AS DateTime), 15, 4)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (49, 2, 1, NULL, CAST(N'2025-11-19T22:56:21.003' AS DateTime), CAST(N'2025-12-03T00:09:31.713' AS DateTime), 15, 2)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (50, 3, 1, NULL, CAST(N'2025-11-19T22:56:21.003' AS DateTime), CAST(N'2025-12-03T02:03:03.197' AS DateTime), 15, 7)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (51, 4, 1, NULL, CAST(N'2025-11-19T22:56:21.003' AS DateTime), CAST(N'2025-11-19T22:56:21.003' AS DateTime), 15, 3)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (52, 5, 1, NULL, CAST(N'2025-11-19T22:56:21.003' AS DateTime), CAST(N'2025-11-19T22:56:21.003' AS DateTime), 15, 5)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (53, 6, 1, NULL, CAST(N'2025-11-19T22:56:21.007' AS DateTime), CAST(N'2025-11-19T22:56:21.007' AS DateTime), 15, 6)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (54, 7, 1, NULL, CAST(N'2025-11-19T22:56:21.007' AS DateTime), CAST(N'2025-11-19T22:56:21.007' AS DateTime), 15, 1)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (55, 1, 1, NULL, CAST(N'2025-11-19T22:56:25.063' AS DateTime), CAST(N'2025-11-19T22:56:25.063' AS DateTime), 16, 9)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (56, 2, 1, NULL, CAST(N'2025-11-19T22:56:25.063' AS DateTime), CAST(N'2025-11-19T22:56:25.063' AS DateTime), 16, 8)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (57, 3, 1, NULL, CAST(N'2025-11-19T22:56:25.063' AS DateTime), CAST(N'2025-11-19T22:56:25.063' AS DateTime), 16, 10)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (58, 1, 1, NULL, CAST(N'2025-11-19T22:56:25.093' AS DateTime), CAST(N'2025-11-19T22:56:25.093' AS DateTime), 17, 12)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (59, 2, 1, NULL, CAST(N'2025-11-19T22:56:25.093' AS DateTime), CAST(N'2025-11-19T22:56:25.093' AS DateTime), 17, 11)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (60, 3, 1, NULL, CAST(N'2025-11-19T22:56:25.093' AS DateTime), CAST(N'2025-11-19T22:56:25.093' AS DateTime), 17, 13)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (61, 1, 1, NULL, CAST(N'2025-11-19T22:56:25.120' AS DateTime), CAST(N'2025-11-19T22:56:25.120' AS DateTime), 18, 16)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (62, 2, 1, NULL, CAST(N'2025-11-19T22:56:25.120' AS DateTime), CAST(N'2025-11-19T22:56:25.120' AS DateTime), 18, 14)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (63, 3, 1, NULL, CAST(N'2025-11-19T22:56:25.120' AS DateTime), CAST(N'2025-11-19T22:56:25.120' AS DateTime), 18, 17)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (64, 4, 1, NULL, CAST(N'2025-11-19T22:56:25.120' AS DateTime), CAST(N'2025-11-19T22:56:25.120' AS DateTime), 18, 15)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (65, 1, 1, NULL, CAST(N'2025-11-19T23:00:12.423' AS DateTime), CAST(N'2025-12-02T23:32:50.873' AS DateTime), 19, 4)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (66, 2, 1, NULL, CAST(N'2025-11-19T23:00:12.427' AS DateTime), CAST(N'2025-12-03T00:09:31.713' AS DateTime), 19, 2)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (67, 3, 1, NULL, CAST(N'2025-11-19T23:00:12.427' AS DateTime), CAST(N'2025-12-03T02:03:03.197' AS DateTime), 19, 7)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (68, 4, 1, NULL, CAST(N'2025-11-19T23:00:12.427' AS DateTime), CAST(N'2025-11-19T23:00:12.427' AS DateTime), 19, 3)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (69, 5, 1, NULL, CAST(N'2025-11-19T23:00:12.427' AS DateTime), CAST(N'2025-11-19T23:00:12.427' AS DateTime), 19, 5)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (70, 6, 1, NULL, CAST(N'2025-11-19T23:00:12.427' AS DateTime), CAST(N'2025-11-19T23:00:12.427' AS DateTime), 19, 6)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (71, 7, 1, NULL, CAST(N'2025-11-19T23:00:12.427' AS DateTime), CAST(N'2025-11-19T23:00:12.427' AS DateTime), 19, 1)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (72, 1, 1, NULL, CAST(N'2025-11-19T23:00:12.440' AS DateTime), CAST(N'2025-11-19T23:00:12.440' AS DateTime), 20, 9)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (73, 2, 1, NULL, CAST(N'2025-11-19T23:00:12.440' AS DateTime), CAST(N'2025-11-19T23:00:12.440' AS DateTime), 20, 8)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (74, 3, 1, NULL, CAST(N'2025-11-19T23:00:12.440' AS DateTime), CAST(N'2025-11-19T23:00:12.440' AS DateTime), 20, 10)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (75, 1, 1, NULL, CAST(N'2025-11-19T23:00:12.447' AS DateTime), CAST(N'2025-11-19T23:00:12.447' AS DateTime), 21, 12)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (76, 2, 1, NULL, CAST(N'2025-11-19T23:00:12.447' AS DateTime), CAST(N'2025-11-19T23:00:12.447' AS DateTime), 21, 11)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (77, 3, 1, NULL, CAST(N'2025-11-19T23:00:12.447' AS DateTime), CAST(N'2025-11-19T23:00:12.447' AS DateTime), 21, 13)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (78, 1, 1, NULL, CAST(N'2025-11-19T23:00:12.463' AS DateTime), CAST(N'2025-11-19T23:00:12.463' AS DateTime), 22, 16)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (79, 2, 1, NULL, CAST(N'2025-11-19T23:00:12.467' AS DateTime), CAST(N'2025-11-19T23:00:12.467' AS DateTime), 22, 14)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (80, 3, 1, NULL, CAST(N'2025-11-19T23:00:12.467' AS DateTime), CAST(N'2025-11-19T23:00:12.467' AS DateTime), 22, 17)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (81, 4, 1, NULL, CAST(N'2025-11-19T23:00:12.467' AS DateTime), CAST(N'2025-11-19T23:00:12.467' AS DateTime), 22, 15)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (82, 1, 1, NULL, CAST(N'2025-11-21T01:28:33.307' AS DateTime), CAST(N'2025-12-02T23:32:50.873' AS DateTime), 23, 4)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (83, 2, 1, NULL, CAST(N'2025-11-21T01:28:33.313' AS DateTime), CAST(N'2025-12-03T00:09:31.713' AS DateTime), 23, 2)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (84, 3, 1, NULL, CAST(N'2025-11-21T01:28:33.313' AS DateTime), CAST(N'2025-12-03T02:03:03.197' AS DateTime), 23, 7)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (85, 4, 1, NULL, CAST(N'2025-11-21T01:28:33.313' AS DateTime), CAST(N'2025-11-21T01:28:33.313' AS DateTime), 23, 3)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (86, 5, 1, NULL, CAST(N'2025-11-21T01:28:33.313' AS DateTime), CAST(N'2025-11-21T01:28:33.313' AS DateTime), 23, 5)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (87, 6, 1, NULL, CAST(N'2025-11-21T01:28:33.313' AS DateTime), CAST(N'2025-11-21T01:28:33.313' AS DateTime), 23, 6)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (88, 7, 1, NULL, CAST(N'2025-11-21T01:28:33.313' AS DateTime), CAST(N'2025-11-21T01:28:33.313' AS DateTime), 23, 1)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (89, 1, 1, NULL, CAST(N'2025-11-21T01:28:33.340' AS DateTime), CAST(N'2025-11-21T01:28:33.340' AS DateTime), 24, 9)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (90, 2, 1, NULL, CAST(N'2025-11-21T01:28:33.343' AS DateTime), CAST(N'2025-11-21T01:28:33.343' AS DateTime), 24, 8)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (91, 3, 1, NULL, CAST(N'2025-11-21T01:28:33.343' AS DateTime), CAST(N'2025-11-21T01:28:33.343' AS DateTime), 24, 10)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (92, 1, 1, NULL, CAST(N'2025-11-21T01:28:33.357' AS DateTime), CAST(N'2025-11-21T01:28:33.357' AS DateTime), 25, 12)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (93, 2, 1, NULL, CAST(N'2025-11-21T01:28:33.360' AS DateTime), CAST(N'2025-11-21T01:28:33.360' AS DateTime), 25, 11)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (94, 3, 1, NULL, CAST(N'2025-11-21T01:28:33.360' AS DateTime), CAST(N'2025-11-21T01:28:33.360' AS DateTime), 25, 13)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (95, 1, 1, NULL, CAST(N'2025-11-21T01:28:33.377' AS DateTime), CAST(N'2025-11-21T01:28:33.377' AS DateTime), 26, 16)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (96, 2, 1, NULL, CAST(N'2025-11-21T01:28:33.377' AS DateTime), CAST(N'2025-11-21T01:28:33.377' AS DateTime), 26, 14)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (97, 3, 1, NULL, CAST(N'2025-11-21T01:28:33.377' AS DateTime), CAST(N'2025-11-21T01:28:33.377' AS DateTime), 26, 17)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (98, 4, 1, NULL, CAST(N'2025-11-21T01:28:33.377' AS DateTime), CAST(N'2025-11-21T01:28:33.377' AS DateTime), 26, 15)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (99, 1, 1, NULL, CAST(N'2025-11-21T01:51:50.493' AS DateTime), CAST(N'2025-12-02T23:32:50.873' AS DateTime), 27, 4)
GO
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (100, 2, 1, NULL, CAST(N'2025-11-21T01:51:50.497' AS DateTime), CAST(N'2025-12-03T00:09:31.713' AS DateTime), 27, 2)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (101, 3, 1, NULL, CAST(N'2025-11-21T01:51:50.497' AS DateTime), CAST(N'2025-12-03T02:03:03.197' AS DateTime), 27, 7)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (102, 4, 1, NULL, CAST(N'2025-11-21T01:51:50.500' AS DateTime), CAST(N'2025-11-21T01:51:50.500' AS DateTime), 27, 3)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (103, 5, 1, NULL, CAST(N'2025-11-21T01:51:50.500' AS DateTime), CAST(N'2025-11-21T01:51:50.500' AS DateTime), 27, 5)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (104, 6, 1, NULL, CAST(N'2025-11-21T01:51:50.500' AS DateTime), CAST(N'2025-11-21T01:51:50.500' AS DateTime), 27, 6)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (105, 7, 1, NULL, CAST(N'2025-11-21T01:51:50.500' AS DateTime), CAST(N'2025-11-21T01:51:50.500' AS DateTime), 27, 1)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (106, 1, 1, NULL, CAST(N'2025-11-21T01:51:50.517' AS DateTime), CAST(N'2025-11-21T01:51:50.517' AS DateTime), 28, 9)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (107, 2, 1, NULL, CAST(N'2025-11-21T01:51:50.517' AS DateTime), CAST(N'2025-11-21T01:51:50.517' AS DateTime), 28, 8)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (108, 3, 1, NULL, CAST(N'2025-11-21T01:51:50.517' AS DateTime), CAST(N'2025-11-21T01:51:50.517' AS DateTime), 28, 10)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (109, 1, 1, NULL, CAST(N'2025-11-21T01:51:50.523' AS DateTime), CAST(N'2025-11-21T01:51:50.523' AS DateTime), 29, 12)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (110, 2, 1, NULL, CAST(N'2025-11-21T01:51:50.523' AS DateTime), CAST(N'2025-11-21T01:51:50.523' AS DateTime), 29, 11)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (111, 3, 1, NULL, CAST(N'2025-11-21T01:51:50.523' AS DateTime), CAST(N'2025-11-21T01:51:50.523' AS DateTime), 29, 13)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (112, 1, 1, NULL, CAST(N'2025-11-21T01:51:50.527' AS DateTime), CAST(N'2025-11-21T01:51:50.527' AS DateTime), 30, 16)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (113, 2, 1, NULL, CAST(N'2025-11-21T01:51:50.527' AS DateTime), CAST(N'2025-11-21T01:51:50.527' AS DateTime), 30, 14)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (114, 3, 1, NULL, CAST(N'2025-11-21T01:51:50.530' AS DateTime), CAST(N'2025-11-21T01:51:50.530' AS DateTime), 30, 17)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (115, 4, 1, NULL, CAST(N'2025-11-21T01:51:50.530' AS DateTime), CAST(N'2025-11-21T01:51:50.530' AS DateTime), 30, 15)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (116, 1, 1, NULL, CAST(N'2025-11-22T12:59:44.993' AS DateTime), CAST(N'2025-12-02T23:32:50.873' AS DateTime), 31, 4)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (117, 2, 1, NULL, CAST(N'2025-11-22T12:59:45.000' AS DateTime), CAST(N'2025-12-03T00:09:31.713' AS DateTime), 31, 2)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (118, 3, 1, NULL, CAST(N'2025-11-22T12:59:45.000' AS DateTime), CAST(N'2025-12-03T02:03:03.197' AS DateTime), 31, 7)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (119, 4, 1, NULL, CAST(N'2025-11-22T12:59:45.000' AS DateTime), CAST(N'2025-11-22T12:59:45.000' AS DateTime), 31, 3)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (120, 5, 1, NULL, CAST(N'2025-11-22T12:59:45.000' AS DateTime), CAST(N'2025-11-22T12:59:45.000' AS DateTime), 31, 5)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (121, 6, 1, NULL, CAST(N'2025-11-22T12:59:45.000' AS DateTime), CAST(N'2025-11-22T12:59:45.000' AS DateTime), 31, 6)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (122, 7, 1, NULL, CAST(N'2025-11-22T12:59:45.000' AS DateTime), CAST(N'2025-11-22T12:59:45.000' AS DateTime), 31, 1)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (123, 1, 1, NULL, CAST(N'2025-11-22T12:59:45.017' AS DateTime), CAST(N'2025-11-22T12:59:45.017' AS DateTime), 32, 9)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (124, 2, 1, NULL, CAST(N'2025-11-22T12:59:45.017' AS DateTime), CAST(N'2025-11-22T12:59:45.017' AS DateTime), 32, 8)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (125, 3, 1, NULL, CAST(N'2025-11-22T12:59:45.017' AS DateTime), CAST(N'2025-11-22T12:59:45.017' AS DateTime), 32, 10)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (126, 1, 1, NULL, CAST(N'2025-11-22T12:59:45.043' AS DateTime), CAST(N'2025-11-22T12:59:45.043' AS DateTime), 33, 12)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (127, 2, 1, NULL, CAST(N'2025-11-22T12:59:45.043' AS DateTime), CAST(N'2025-11-22T12:59:45.043' AS DateTime), 33, 11)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (128, 3, 1, NULL, CAST(N'2025-11-22T12:59:45.043' AS DateTime), CAST(N'2025-11-22T12:59:45.043' AS DateTime), 33, 13)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (129, 1, 1, NULL, CAST(N'2025-11-22T12:59:45.047' AS DateTime), CAST(N'2025-11-22T12:59:45.047' AS DateTime), 34, 16)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (130, 2, 1, NULL, CAST(N'2025-11-22T12:59:45.047' AS DateTime), CAST(N'2025-11-22T12:59:45.047' AS DateTime), 34, 14)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (131, 3, 1, NULL, CAST(N'2025-11-22T12:59:45.050' AS DateTime), CAST(N'2025-11-22T12:59:45.050' AS DateTime), 34, 17)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (132, 4, 1, NULL, CAST(N'2025-11-22T12:59:45.050' AS DateTime), CAST(N'2025-11-22T12:59:45.050' AS DateTime), 34, 15)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (133, 1, 1, NULL, CAST(N'2025-11-22T16:18:39.163' AS DateTime), CAST(N'2025-12-02T23:32:50.873' AS DateTime), 35, 4)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (134, 2, 1, NULL, CAST(N'2025-11-22T16:18:39.167' AS DateTime), CAST(N'2025-12-03T00:09:31.713' AS DateTime), 35, 2)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (135, 3, 1, NULL, CAST(N'2025-11-22T16:18:39.167' AS DateTime), CAST(N'2025-12-03T02:03:03.197' AS DateTime), 35, 7)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (136, 4, 1, NULL, CAST(N'2025-11-22T16:18:39.167' AS DateTime), CAST(N'2025-11-22T16:18:39.167' AS DateTime), 35, 3)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (137, 5, 1, NULL, CAST(N'2025-11-22T16:18:39.170' AS DateTime), CAST(N'2025-11-22T16:18:39.170' AS DateTime), 35, 5)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (138, 6, 1, NULL, CAST(N'2025-11-22T16:18:39.170' AS DateTime), CAST(N'2025-11-22T16:18:39.170' AS DateTime), 35, 6)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (139, 7, 1, NULL, CAST(N'2025-11-22T16:18:39.170' AS DateTime), CAST(N'2025-11-22T16:18:39.170' AS DateTime), 35, 1)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (140, 1, 1, NULL, CAST(N'2025-11-22T16:18:39.190' AS DateTime), CAST(N'2025-11-22T16:18:39.190' AS DateTime), 36, 9)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (141, 2, 1, NULL, CAST(N'2025-11-22T16:18:39.190' AS DateTime), CAST(N'2025-11-22T16:18:39.190' AS DateTime), 36, 8)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (142, 3, 1, NULL, CAST(N'2025-11-22T16:18:39.193' AS DateTime), CAST(N'2025-11-22T16:18:39.193' AS DateTime), 36, 10)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (143, 1, 1, NULL, CAST(N'2025-11-22T16:18:39.197' AS DateTime), CAST(N'2025-11-22T16:18:39.197' AS DateTime), 37, 12)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (144, 2, 1, NULL, CAST(N'2025-11-22T16:18:39.197' AS DateTime), CAST(N'2025-11-22T16:18:39.197' AS DateTime), 37, 11)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (145, 3, 1, NULL, CAST(N'2025-11-22T16:18:39.200' AS DateTime), CAST(N'2025-11-22T16:18:39.200' AS DateTime), 37, 13)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (146, 1, 1, NULL, CAST(N'2025-11-22T16:18:39.203' AS DateTime), CAST(N'2025-11-22T16:18:39.203' AS DateTime), 38, 16)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (147, 2, 1, NULL, CAST(N'2025-11-22T16:18:39.207' AS DateTime), CAST(N'2025-11-22T16:18:39.207' AS DateTime), 38, 14)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (148, 3, 1, NULL, CAST(N'2025-11-22T16:18:39.207' AS DateTime), CAST(N'2025-11-22T16:18:39.207' AS DateTime), 38, 17)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (149, 4, 1, NULL, CAST(N'2025-11-22T16:18:39.207' AS DateTime), CAST(N'2025-11-22T16:18:39.207' AS DateTime), 38, 15)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (150, 1, 1, NULL, CAST(N'2025-11-30T13:20:21.357' AS DateTime), CAST(N'2025-12-02T23:32:50.873' AS DateTime), 39, 4)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (151, 2, 1, NULL, CAST(N'2025-11-30T13:20:21.360' AS DateTime), CAST(N'2025-12-03T00:09:31.713' AS DateTime), 39, 2)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (152, 3, 1, NULL, CAST(N'2025-11-30T13:20:21.363' AS DateTime), CAST(N'2025-12-03T02:03:03.197' AS DateTime), 39, 7)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (153, 4, 1, NULL, CAST(N'2025-11-30T13:20:21.363' AS DateTime), CAST(N'2025-11-30T13:20:21.363' AS DateTime), 39, 3)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (154, 5, 1, NULL, CAST(N'2025-11-30T13:20:21.363' AS DateTime), CAST(N'2025-11-30T13:20:21.363' AS DateTime), 39, 5)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (155, 6, 1, NULL, CAST(N'2025-11-30T13:20:21.363' AS DateTime), CAST(N'2025-11-30T13:20:21.363' AS DateTime), 39, 6)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (156, 7, 1, NULL, CAST(N'2025-11-30T13:20:21.363' AS DateTime), CAST(N'2025-11-30T13:20:21.363' AS DateTime), 39, 1)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (157, 1, 1, NULL, CAST(N'2025-11-30T13:20:21.380' AS DateTime), CAST(N'2025-11-30T13:20:21.380' AS DateTime), 40, 9)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (158, 2, 1, NULL, CAST(N'2025-11-30T13:20:21.380' AS DateTime), CAST(N'2025-11-30T13:20:21.380' AS DateTime), 40, 8)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (159, 3, 1, NULL, CAST(N'2025-11-30T13:20:21.380' AS DateTime), CAST(N'2025-11-30T13:20:21.380' AS DateTime), 40, 10)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (160, 1, 1, NULL, CAST(N'2025-11-30T13:20:21.387' AS DateTime), CAST(N'2025-11-30T13:20:21.387' AS DateTime), 41, 12)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (161, 2, 1, NULL, CAST(N'2025-11-30T13:20:21.387' AS DateTime), CAST(N'2025-11-30T13:20:21.387' AS DateTime), 41, 11)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (162, 3, 1, NULL, CAST(N'2025-11-30T13:20:21.387' AS DateTime), CAST(N'2025-11-30T13:20:21.387' AS DateTime), 41, 13)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (163, 1, 1, NULL, CAST(N'2025-11-30T13:20:21.393' AS DateTime), CAST(N'2025-11-30T13:20:21.393' AS DateTime), 42, 16)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (164, 2, 1, NULL, CAST(N'2025-11-30T13:20:21.397' AS DateTime), CAST(N'2025-11-30T13:20:21.397' AS DateTime), 42, 14)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (165, 3, 1, NULL, CAST(N'2025-11-30T13:20:21.397' AS DateTime), CAST(N'2025-11-30T13:20:21.397' AS DateTime), 42, 17)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (166, 4, 1, NULL, CAST(N'2025-11-30T13:20:21.397' AS DateTime), CAST(N'2025-11-30T13:20:21.397' AS DateTime), 42, 15)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (167, 1, 1, NULL, CAST(N'2025-12-01T11:30:40.580' AS DateTime), CAST(N'2025-12-02T23:32:50.873' AS DateTime), 43, 4)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (168, 2, 1, NULL, CAST(N'2025-12-01T11:30:40.583' AS DateTime), CAST(N'2025-12-03T00:09:31.713' AS DateTime), 43, 2)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (169, 3, 1, NULL, CAST(N'2025-12-01T11:30:40.583' AS DateTime), CAST(N'2025-12-03T02:03:03.197' AS DateTime), 43, 7)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (170, 4, 1, NULL, CAST(N'2025-12-01T11:30:40.587' AS DateTime), CAST(N'2025-12-01T11:30:40.587' AS DateTime), 43, 3)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (171, 5, 1, NULL, CAST(N'2025-12-01T11:30:40.587' AS DateTime), CAST(N'2025-12-01T11:30:40.587' AS DateTime), 43, 5)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (172, 6, 1, NULL, CAST(N'2025-12-01T11:30:40.587' AS DateTime), CAST(N'2025-12-01T11:30:40.587' AS DateTime), 43, 6)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (173, 7, 1, NULL, CAST(N'2025-12-01T11:30:40.587' AS DateTime), CAST(N'2025-12-01T11:30:40.587' AS DateTime), 43, 1)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (174, 1, 1, NULL, CAST(N'2025-12-01T11:30:40.603' AS DateTime), CAST(N'2025-12-01T11:30:40.603' AS DateTime), 44, 9)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (175, 2, 1, NULL, CAST(N'2025-12-01T11:30:40.603' AS DateTime), CAST(N'2025-12-01T11:30:40.603' AS DateTime), 44, 8)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (176, 3, 1, NULL, CAST(N'2025-12-01T11:30:40.603' AS DateTime), CAST(N'2025-12-01T11:30:40.603' AS DateTime), 44, 10)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (177, 1, 1, NULL, CAST(N'2025-12-01T11:30:40.610' AS DateTime), CAST(N'2025-12-01T11:30:40.610' AS DateTime), 45, 12)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (178, 2, 1, NULL, CAST(N'2025-12-01T11:30:40.610' AS DateTime), CAST(N'2025-12-01T11:30:40.610' AS DateTime), 45, 11)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (179, 3, 1, NULL, CAST(N'2025-12-01T11:30:40.610' AS DateTime), CAST(N'2025-12-01T11:30:40.610' AS DateTime), 45, 13)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (180, 1, 1, NULL, CAST(N'2025-12-01T11:30:40.613' AS DateTime), CAST(N'2025-12-01T11:30:40.613' AS DateTime), 46, 16)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (181, 2, 1, NULL, CAST(N'2025-12-01T11:30:40.617' AS DateTime), CAST(N'2025-12-01T11:30:40.617' AS DateTime), 46, 14)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (182, 3, 1, NULL, CAST(N'2025-12-01T11:30:40.617' AS DateTime), CAST(N'2025-12-01T11:30:40.617' AS DateTime), 46, 17)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (183, 4, 1, NULL, CAST(N'2025-12-01T11:30:40.617' AS DateTime), CAST(N'2025-12-01T11:30:40.617' AS DateTime), 46, 15)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (184, 1, 1, NULL, CAST(N'2025-12-02T01:03:59.400' AS DateTime), CAST(N'2025-12-02T23:32:50.873' AS DateTime), 47, 4)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (185, 2, 1, NULL, CAST(N'2025-12-02T01:03:59.403' AS DateTime), CAST(N'2025-12-03T00:09:31.713' AS DateTime), 47, 2)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (186, 3, 1, NULL, CAST(N'2025-12-02T01:03:59.403' AS DateTime), CAST(N'2025-12-02T01:03:59.403' AS DateTime), 47, 3)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (187, 4, 1, NULL, CAST(N'2025-12-02T01:03:59.403' AS DateTime), CAST(N'2025-12-02T01:03:59.403' AS DateTime), 47, 5)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (188, 5, 1, NULL, CAST(N'2025-12-02T01:03:59.403' AS DateTime), CAST(N'2025-12-02T01:03:59.403' AS DateTime), 47, 6)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (189, 6, 1, NULL, CAST(N'2025-12-02T01:03:59.407' AS DateTime), CAST(N'2025-12-02T01:03:59.407' AS DateTime), 47, 1)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (190, 1, 1, NULL, CAST(N'2025-12-02T01:03:59.430' AS DateTime), CAST(N'2025-12-02T01:03:59.430' AS DateTime), 48, 9)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (191, 2, 1, NULL, CAST(N'2025-12-02T01:03:59.430' AS DateTime), CAST(N'2025-12-02T01:03:59.430' AS DateTime), 48, 8)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (192, 3, 1, NULL, CAST(N'2025-12-02T01:03:59.430' AS DateTime), CAST(N'2025-12-02T01:03:59.430' AS DateTime), 48, 10)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (193, 1, 1, NULL, CAST(N'2025-12-02T01:03:59.447' AS DateTime), CAST(N'2025-12-02T01:03:59.447' AS DateTime), 49, 12)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (194, 2, 1, NULL, CAST(N'2025-12-02T01:03:59.450' AS DateTime), CAST(N'2025-12-02T01:03:59.450' AS DateTime), 49, 11)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (195, 3, 1, NULL, CAST(N'2025-12-02T01:03:59.450' AS DateTime), CAST(N'2025-12-02T01:03:59.450' AS DateTime), 49, 13)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (196, 1, 1, NULL, CAST(N'2025-12-02T01:03:59.473' AS DateTime), CAST(N'2025-12-02T01:03:59.473' AS DateTime), 50, 16)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (197, 2, 1, NULL, CAST(N'2025-12-02T01:03:59.473' AS DateTime), CAST(N'2025-12-02T01:03:59.473' AS DateTime), 50, 14)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (198, 3, 1, NULL, CAST(N'2025-12-02T01:03:59.473' AS DateTime), CAST(N'2025-12-02T01:03:59.473' AS DateTime), 50, 17)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (199, 4, 1, NULL, CAST(N'2025-12-02T01:03:59.473' AS DateTime), CAST(N'2025-12-02T01:03:59.473' AS DateTime), 50, 15)
GO
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (201, 4, 1, NULL, CAST(N'2025-12-02T23:35:00.673' AS DateTime), CAST(N'2025-12-02T23:35:00.673' AS DateTime), 48, 22)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (203, 2, 1, NULL, CAST(N'2025-12-03T00:22:00.083' AS DateTime), CAST(N'2025-12-03T00:22:00.083' AS DateTime), 51, 4)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (204, 3, 1, NULL, CAST(N'2025-12-03T00:22:00.083' AS DateTime), CAST(N'2025-12-03T00:22:00.083' AS DateTime), 51, 2)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (205, 4, 1, NULL, CAST(N'2025-12-03T00:22:00.083' AS DateTime), CAST(N'2025-12-03T00:22:00.083' AS DateTime), 51, 3)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (206, 5, 1, NULL, CAST(N'2025-12-03T00:22:00.087' AS DateTime), CAST(N'2025-12-03T00:22:00.087' AS DateTime), 51, 5)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (207, 6, 1, NULL, CAST(N'2025-12-03T00:22:00.087' AS DateTime), CAST(N'2025-12-03T00:22:00.087' AS DateTime), 51, 6)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (208, 7, 1, NULL, CAST(N'2025-12-03T00:22:00.087' AS DateTime), CAST(N'2025-12-03T00:22:00.087' AS DateTime), 51, 1)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (211, 2, 1, NULL, CAST(N'2025-12-03T00:22:00.103' AS DateTime), CAST(N'2025-12-03T00:22:00.103' AS DateTime), 52, 22)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (212, 3, 1, NULL, CAST(N'2025-12-03T00:22:00.107' AS DateTime), CAST(N'2025-12-03T00:22:00.107' AS DateTime), 52, 9)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (214, 5, 1, NULL, CAST(N'2025-12-03T00:22:00.107' AS DateTime), CAST(N'2025-12-03T00:22:00.107' AS DateTime), 52, 8)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (215, 6, 1, NULL, CAST(N'2025-12-03T00:22:00.107' AS DateTime), CAST(N'2025-12-03T00:22:00.107' AS DateTime), 52, 10)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (216, 1, 1, NULL, CAST(N'2025-12-03T00:22:00.117' AS DateTime), CAST(N'2025-12-03T00:22:00.117' AS DateTime), 53, 12)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (217, 2, 1, NULL, CAST(N'2025-12-03T00:22:00.117' AS DateTime), CAST(N'2025-12-03T00:22:00.117' AS DateTime), 53, 11)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (218, 3, 1, NULL, CAST(N'2025-12-03T00:22:00.117' AS DateTime), CAST(N'2025-12-03T00:22:00.117' AS DateTime), 53, 13)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (219, 1, 1, NULL, CAST(N'2025-12-03T00:22:00.123' AS DateTime), CAST(N'2025-12-03T00:22:00.123' AS DateTime), 54, 16)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (220, 2, 1, NULL, CAST(N'2025-12-03T00:22:00.127' AS DateTime), CAST(N'2025-12-03T00:22:00.127' AS DateTime), 54, 14)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (221, 3, 1, NULL, CAST(N'2025-12-03T00:22:00.127' AS DateTime), CAST(N'2025-12-03T00:22:00.127' AS DateTime), 54, 17)
INSERT [dbo].[CategoryDish] ([CategoryDishID], [DisplayOrder], [IsAvailable], [Note], [CreatedAt], [UpdatedAt], [MenuCategoryID], [DishID]) VALUES (222, 4, 1, NULL, CAST(N'2025-12-03T00:22:00.127' AS DateTime), CAST(N'2025-12-03T00:22:00.127' AS DateTime), 54, 15)
SET IDENTITY_INSERT [dbo].[CategoryDish] OFF
GO
SET IDENTITY_INSERT [dbo].[Customers] ON 

INSERT [dbo].[Customers] ([CustomerID], [Name], [PhoneNumber], [Email], [Gender], [DateOfBirth], [Address], [LoyaltyPoints], [CreatedAt], [UpdatedAt], [IsActive], [Username], [Password], [CreditPoints]) VALUES (1, N'Nguyễn Thị Lan', N'0912345678', N'lan.nguyen@gmail.com', N'Nữ', CAST(N'1990-05-15' AS Date), N'123 Lê Lợi, Q1, TP.HCM', 1200, CAST(N'2025-11-16T00:22:27.330' AS DateTime), CAST(N'2025-12-02T16:09:30.853' AS DateTime), 1, N'lan.nguyen', N'123456', 0)
INSERT [dbo].[Customers] ([CustomerID], [Name], [PhoneNumber], [Email], [Gender], [DateOfBirth], [Address], [LoyaltyPoints], [CreatedAt], [UpdatedAt], [IsActive], [Username], [Password], [CreditPoints]) VALUES (2, N'Trần Văn Minh', N'0987654321', N'minh.tran@gmail.com', N'Nam', CAST(N'1985-08-20' AS Date), N'456 Trần Hưng Đạo, Q5, TP.HCM', 850, CAST(N'2025-11-16T00:22:27.330' AS DateTime), CAST(N'2025-12-02T16:10:17.183' AS DateTime), 1, N'minh.tran', N'123456', 0)
INSERT [dbo].[Customers] ([CustomerID], [Name], [PhoneNumber], [Email], [Gender], [DateOfBirth], [Address], [LoyaltyPoints], [CreatedAt], [UpdatedAt], [IsActive], [Username], [Password], [CreditPoints]) VALUES (3, N'Phạm Thị Hoa', N'0909123456', N'hoa.pham@gmail.com', N'Nữ', CAST(N'1995-12-10' AS Date), N'789 Nguyễn Trãi, Q1, TP.HCM', 500, CAST(N'2025-11-16T00:22:27.330' AS DateTime), CAST(N'2025-11-19T23:32:53.157' AS DateTime), 1, N'hoa.pham', N'123456', 0)
INSERT [dbo].[Customers] ([CustomerID], [Name], [PhoneNumber], [Email], [Gender], [DateOfBirth], [Address], [LoyaltyPoints], [CreatedAt], [UpdatedAt], [IsActive], [Username], [Password], [CreditPoints]) VALUES (4, N'Lê Quốc Bảo', N'0918765432', N'bao.le@gmail.com', N'Nam', CAST(N'1988-03-25' AS Date), N'45 Võ Văn Tần, Q3, TP.HCM', 320, CAST(N'2025-11-18T14:22:31.600' AS DateTime), CAST(N'2025-11-19T23:32:53.157' AS DateTime), 1, N'bao.le', N'123456', 0)
INSERT [dbo].[Customers] ([CustomerID], [Name], [PhoneNumber], [Email], [Gender], [DateOfBirth], [Address], [LoyaltyPoints], [CreatedAt], [UpdatedAt], [IsActive], [Username], [Password], [CreditPoints]) VALUES (5, N'Đỗ Thị Mai', N'0913456789', N'mai.do@yahoo.com', N'Nữ', CAST(N'1992-07-18' AS Date), N'67 Lý Chính Thắng, Q3, TP.HCM', 680, CAST(N'2025-11-18T14:22:31.600' AS DateTime), CAST(N'2025-11-19T23:32:53.157' AS DateTime), 1, N'mai.do', N'123456', 0)
INSERT [dbo].[Customers] ([CustomerID], [Name], [PhoneNumber], [Email], [Gender], [DateOfBirth], [Address], [LoyaltyPoints], [CreatedAt], [UpdatedAt], [IsActive], [Username], [Password], [CreditPoints]) VALUES (6, N'Vũ Hoàng Long', N'0925678901', N'long.vu@outlook.com', N'Nam', CAST(N'1987-11-05' AS Date), N'89 Hai Bà Trưng, Q1, TP.HCM', 1450, CAST(N'2025-11-18T14:22:31.600' AS DateTime), CAST(N'2025-11-19T23:32:53.157' AS DateTime), 1, N'long.vu', N'123456', 0)
INSERT [dbo].[Customers] ([CustomerID], [Name], [PhoneNumber], [Email], [Gender], [DateOfBirth], [Address], [LoyaltyPoints], [CreatedAt], [UpdatedAt], [IsActive], [Username], [Password], [CreditPoints]) VALUES (7, N'Hoàng Thị Kim', N'0936789012', N'kim.hoang@gmail.com', N'Nữ', CAST(N'1993-09-30' AS Date), N'12 Pasteur, Q1, TP.HCM', 280, CAST(N'2025-11-18T14:22:31.600' AS DateTime), CAST(N'2025-11-19T23:32:53.157' AS DateTime), 1, N'kim.hoang', N'123456', 0)
INSERT [dbo].[Customers] ([CustomerID], [Name], [PhoneNumber], [Email], [Gender], [DateOfBirth], [Address], [LoyaltyPoints], [CreatedAt], [UpdatedAt], [IsActive], [Username], [Password], [CreditPoints]) VALUES (8, N'Phan Minh Tuấn', N'0947890123', N'tuan.phan@gmail.com', N'Nam', CAST(N'1990-02-14' AS Date), N'34 Đinh Tiên Hoàng, Q1, TP.HCM', 920, CAST(N'2025-11-18T14:22:31.600' AS DateTime), CAST(N'2025-11-19T23:32:53.157' AS DateTime), 1, N'tuan.phan', N'123456', 0)
INSERT [dbo].[Customers] ([CustomerID], [Name], [PhoneNumber], [Email], [Gender], [DateOfBirth], [Address], [LoyaltyPoints], [CreatedAt], [UpdatedAt], [IsActive], [Username], [Password], [CreditPoints]) VALUES (9, N'Nguyễn Vinh Quang', N'0343000273', N'nguyenvinhquang9a8@gmail.com', NULL, NULL, NULL, 8075, CAST(N'2025-11-21T02:42:04.120' AS DateTime), CAST(N'2025-12-03T01:34:17.417' AS DateTime), 1, N'quang', N'123456', 9400)
SET IDENTITY_INSERT [dbo].[Customers] OFF
GO
SET IDENTITY_INSERT [dbo].[DiningTables] ON 

INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (1, 2, NULL, N'QR-Q1-B01', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-12-03T01:03:59.107' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (2, 2, NULL, N'QR-Q1-B02', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (3, 2, NULL, N'QR-Q1-B03', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (4, 2, NULL, N'QR-Q1-B04', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (5, 2, NULL, N'QR-Q1-B05', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (6, 4, NULL, N'QR-Q1-B06', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (7, 4, NULL, N'QR-Q1-B07', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (8, 4, NULL, N'QR-Q1-B08', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (9, 4, NULL, N'QR-Q1-B09', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (10, 4, NULL, N'QR-Q1-B10', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (11, 4, NULL, N'QR-Q1-B11', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (12, 4, NULL, N'QR-Q1-B12', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (13, 4, NULL, N'QR-Q1-B13', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (14, 4, NULL, N'QR-Q1-B14', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (15, 4, NULL, N'QR-Q1-B15', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (16, 6, NULL, N'QR-Q1-B16', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (17, 6, NULL, N'QR-Q1-B17', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (18, 6, NULL, N'QR-Q1-B18', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (19, 8, NULL, N'QR-Q1-B19', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (20, 8, NULL, N'QR-Q1-B20', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 1, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (21, 2, NULL, N'QR-GV-B01', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 2, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (22, 2, NULL, N'QR-GV-B02', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 2, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (23, 2, NULL, N'QR-GV-B03', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 2, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (24, 4, NULL, N'QR-GV-B04', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 2, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (25, 4, NULL, N'QR-GV-B05', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 2, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (26, 4, NULL, N'QR-GV-B06', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 2, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (27, 4, NULL, N'QR-GV-B07', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 2, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (28, 4, NULL, N'QR-GV-B08', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 2, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (29, 4, NULL, N'QR-GV-B09', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 2, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (30, 4, NULL, N'QR-GV-B10', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 2, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (31, 4, NULL, N'QR-GV-B11', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 2, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (32, 6, NULL, N'QR-GV-B12', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 2, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (33, 6, NULL, N'QR-GV-B13', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 2, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (34, 6, NULL, N'QR-GV-B14', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 2, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (35, 8, NULL, N'QR-GV-B15', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 2, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (36, 2, NULL, N'QR-BT-B01', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 3, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (37, 2, NULL, N'QR-BT-B02', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 3, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (38, 4, NULL, N'QR-BT-B03', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 3, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (39, 4, NULL, N'QR-BT-B04', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 3, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (40, 4, NULL, N'QR-BT-B05', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 3, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (41, 4, NULL, N'QR-BT-B06', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 3, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (42, 4, NULL, N'QR-BT-B07', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 3, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (43, 4, NULL, N'QR-BT-B08', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 3, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (44, 6, NULL, N'QR-BT-B09', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 3, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (45, 6, NULL, N'QR-BT-B10', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 3, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (46, 6, NULL, N'QR-BT-B11', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 3, 1)
INSERT [dbo].[DiningTables] ([TableID], [NumberOfSeats], [CurrentOrderID], [QRCode], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [StatusID]) VALUES (47, 8, NULL, N'QR-BT-B12', 1, CAST(N'2025-11-19T22:50:17.630' AS DateTime), CAST(N'2025-11-19T22:50:17.630' AS DateTime), 3, 1)
SET IDENTITY_INSERT [dbo].[DiningTables] OFF
GO
SET IDENTITY_INSERT [dbo].[Dishes] ON 

INSERT [dbo].[Dishes] ([DishID], [Name], [Price], [Available], [Image], [Description], [Unit], [IsVegetarian], [IsDailySpecial], [IsActive], [CreatedAt], [UpdatedAt], [CategoryID]) VALUES (1, N'Phở Bò Đặc Biệt', CAST(75000.00 AS Decimal(15, 2)), 1, N'/Images/dish_20251203014802250.jpg', N'Phở bò truyền thống Hà Nội với nước dùng hầm xương 12 tiếng', N'Tô', 0, 1, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T01:48:02.257' AS DateTime), 1)
INSERT [dbo].[Dishes] ([DishID], [Name], [Price], [Available], [Image], [Description], [Unit], [IsVegetarian], [IsDailySpecial], [IsActive], [CreatedAt], [UpdatedAt], [CategoryID]) VALUES (2, N'Bún Chả Hà Nội', CAST(60000.00 AS Decimal(15, 2)), 1, N'/Images/dish_20251203014435075.jpg', N'Bún chả đặc sản Hà Nội với chả nướng than hồng', N'Phần', 0, 1, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T01:44:35.080' AS DateTime), 1)
INSERT [dbo].[Dishes] ([DishID], [Name], [Price], [Available], [Image], [Description], [Unit], [IsVegetarian], [IsDailySpecial], [IsActive], [CreatedAt], [UpdatedAt], [CategoryID]) VALUES (3, N'Cơm Sườn Bì Chả', CAST(65000.00 AS Decimal(15, 2)), 1, N'/Images/dish_20251203014504150.jpg', N'Cơm sườn bì chả truyền thống Sài Gòn', N'Phần', 0, 0, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T01:45:04.157' AS DateTime), 1)
INSERT [dbo].[Dishes] ([DishID], [Name], [Price], [Available], [Image], [Description], [Unit], [IsVegetarian], [IsDailySpecial], [IsActive], [CreatedAt], [UpdatedAt], [CategoryID]) VALUES (4, N'Bún Bò Huế', CAST(55000.00 AS Decimal(15, 2)), 1, N'/Images/dish_20251203014409795.jpg', N'Bún bò Huế cay nồng đậm đà', N'Tô', 0, 0, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T01:44:09.800' AS DateTime), 1)
INSERT [dbo].[Dishes] ([DishID], [Name], [Price], [Available], [Image], [Description], [Unit], [IsVegetarian], [IsDailySpecial], [IsActive], [CreatedAt], [UpdatedAt], [CategoryID]) VALUES (5, N'Hủ Tiếu Nam Vang', CAST(50000.00 AS Decimal(15, 2)), 1, N'/Images/dish_20251203014518088.jpg', N'Hủ tiếu Nam Vang đặc biệt', N'Tô', 0, 0, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T01:45:18.093' AS DateTime), 1)
INSERT [dbo].[Dishes] ([DishID], [Name], [Price], [Available], [Image], [Description], [Unit], [IsVegetarian], [IsDailySpecial], [IsActive], [CreatedAt], [UpdatedAt], [CategoryID]) VALUES (6, N'Mì Xào Bò', CAST(60000.00 AS Decimal(15, 2)), 1, N'/Images/dish_20251203014537428.jpg', N'Mì xào bò đậm đà hương vị', N'Phần', 0, 0, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T01:45:37.433' AS DateTime), 1)
INSERT [dbo].[Dishes] ([DishID], [Name], [Price], [Available], [Image], [Description], [Unit], [IsVegetarian], [IsDailySpecial], [IsActive], [CreatedAt], [UpdatedAt], [CategoryID]) VALUES (7, N'Cơm Gà Xối Mỡ', CAST(70000.00 AS Decimal(15, 2)), 1, N'/Images/dish_20251203014448955.jpg', N'Cơm gà Hải Nam xối mỡ thơm ngon', N'Phần', 0, 1, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T02:03:03.197' AS DateTime), 1)
INSERT [dbo].[Dishes] ([DishID], [Name], [Price], [Available], [Image], [Description], [Unit], [IsVegetarian], [IsDailySpecial], [IsActive], [CreatedAt], [UpdatedAt], [CategoryID]) VALUES (8, N'Nem Rán', CAST(40000.00 AS Decimal(15, 2)), 1, N'/Images/dish_20251203014851937.jpg', N'Nem rán giòn rụm', N'Phần', 0, 0, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T01:48:51.943' AS DateTime), 2)
INSERT [dbo].[Dishes] ([DishID], [Name], [Price], [Available], [Image], [Description], [Unit], [IsVegetarian], [IsDailySpecial], [IsActive], [CreatedAt], [UpdatedAt], [CategoryID]) VALUES (9, N'Gỏi Cuốn', CAST(35000.00 AS Decimal(15, 2)), 1, N'/Images/dish_20251203014835783.jpg', N'Gỏi cuốn tươi ngon', N'Phần', 1, 0, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T01:48:35.790' AS DateTime), 2)
INSERT [dbo].[Dishes] ([DishID], [Name], [Price], [Available], [Image], [Description], [Unit], [IsVegetarian], [IsDailySpecial], [IsActive], [CreatedAt], [UpdatedAt], [CategoryID]) VALUES (10, N'Salad Rau Củ', CAST(30000.00 AS Decimal(15, 2)), 1, N'/Images/dish_20251203014903144.jpg', N'Salad rau củ tươi', N'Phần', 1, 0, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T01:49:03.150' AS DateTime), 2)
INSERT [dbo].[Dishes] ([DishID], [Name], [Price], [Available], [Image], [Description], [Unit], [IsVegetarian], [IsDailySpecial], [IsActive], [CreatedAt], [UpdatedAt], [CategoryID]) VALUES (11, N'Chè Khúc Bạch', CAST(35000.00 AS Decimal(15, 2)), 1, N'/Images/dish_20251203014934154.jpg', N'Chè khúc bạch mát lạnh', N'Ly', 1, 0, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T01:49:34.160' AS DateTime), 3)
INSERT [dbo].[Dishes] ([DishID], [Name], [Price], [Available], [Image], [Description], [Unit], [IsVegetarian], [IsDailySpecial], [IsActive], [CreatedAt], [UpdatedAt], [CategoryID]) VALUES (12, N'Bánh Flan', CAST(30000.00 AS Decimal(15, 2)), 1, N'/Images/dish_20251203014919825.jpg', N'Bánh flan caramen ngọt ngào', N'Cái', 1, 0, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T01:49:19.830' AS DateTime), 3)
INSERT [dbo].[Dishes] ([DishID], [Name], [Price], [Available], [Image], [Description], [Unit], [IsVegetarian], [IsDailySpecial], [IsActive], [CreatedAt], [UpdatedAt], [CategoryID]) VALUES (13, N'Chè Thái', CAST(40000.00 AS Decimal(15, 2)), 1, N'/Images/dish_20251203014944820.jpg', N'Chè Thái nhiều màu sắc', N'Ly', 1, 0, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T01:49:44.830' AS DateTime), 3)
INSERT [dbo].[Dishes] ([DishID], [Name], [Price], [Available], [Image], [Description], [Unit], [IsVegetarian], [IsDailySpecial], [IsActive], [CreatedAt], [UpdatedAt], [CategoryID]) VALUES (14, N'Nước Cam Vắt', CAST(40000.00 AS Decimal(15, 2)), 1, N'/Images/dish_20251203014217645.jpg', N'Nước cam tươi vắt tay', N'Ly', 1, 0, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T01:42:17.650' AS DateTime), 4)
INSERT [dbo].[Dishes] ([DishID], [Name], [Price], [Available], [Image], [Description], [Unit], [IsVegetarian], [IsDailySpecial], [IsActive], [CreatedAt], [UpdatedAt], [CategoryID]) VALUES (15, N'Trà Đá', CAST(10000.00 AS Decimal(15, 2)), 1, N'/Images/dish_20251203014327609.jpg', N'Trà đá miễn phí', N'Ly', 1, 0, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T01:43:27.613' AS DateTime), 4)
INSERT [dbo].[Dishes] ([DishID], [Name], [Price], [Available], [Image], [Description], [Unit], [IsVegetarian], [IsDailySpecial], [IsActive], [CreatedAt], [UpdatedAt], [CategoryID]) VALUES (16, N'Cà Phê Sữa Đá', CAST(40000.00 AS Decimal(15, 2)), 1, N'/Images/dish_20251203014151691.jpg', N'Cà phê sữa đá đậm đà', N'Ly', 1, 0, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T01:41:51.700' AS DateTime), 4)
INSERT [dbo].[Dishes] ([DishID], [Name], [Price], [Available], [Image], [Description], [Unit], [IsVegetarian], [IsDailySpecial], [IsActive], [CreatedAt], [UpdatedAt], [CategoryID]) VALUES (17, N'Sinh Tố Bơ', CAST(45000.00 AS Decimal(15, 2)), 1, N'/Images/dish_20251203014230039.jpg', N'Sinh tố bơ béo ngậy', N'Ly', 1, 0, 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-12-03T01:42:30.043' AS DateTime), 4)
INSERT [dbo].[Dishes] ([DishID], [Name], [Price], [Available], [Image], [Description], [Unit], [IsVegetarian], [IsDailySpecial], [IsActive], [CreatedAt], [UpdatedAt], [CategoryID]) VALUES (22, N'Chả Cá', CAST(15000.00 AS Decimal(15, 2)), 1, N'/Images/dish_20251203014738222.jpg', N'Chả Cá làm từ cá', N'Dĩa', 0, 1, 1, CAST(N'2025-12-02T23:35:00.580' AS DateTime), CAST(N'2025-12-03T01:47:38.230' AS DateTime), 2)
SET IDENTITY_INSERT [dbo].[Dishes] OFF
GO
SET IDENTITY_INSERT [dbo].[DishIngredients] ON 

INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (1, 1, 1, CAST(0.20 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (2, 1, 21, CAST(0.30 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (3, 1, 7, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (4, 1, 8, CAST(0.02 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (5, 1, 9, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (6, 1, 13, CAST(0.02 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (7, 2, 3, CAST(0.15 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (8, 2, 22, CAST(0.30 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (9, 2, 7, CAST(0.08 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (10, 2, 11, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (11, 2, 13, CAST(0.03 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (12, 3, 3, CAST(0.15 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (13, 3, 25, CAST(0.25 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (14, 3, 26, CAST(1.00 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (15, 3, 11, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (16, 3, 14, CAST(0.02 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (17, 4, 1, CAST(0.18 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (18, 4, 22, CAST(0.30 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (19, 4, 7, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (20, 4, 8, CAST(0.02 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (21, 4, 20, CAST(0.01 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (22, 5, 3, CAST(0.10 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (23, 5, 4, CAST(0.08 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (24, 5, 24, CAST(0.30 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (25, 5, 7, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (26, 5, 9, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (27, 6, 1, CAST(0.15 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (28, 6, 23, CAST(0.25 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (29, 6, 12, CAST(0.08 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (30, 6, 10, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (31, 6, 14, CAST(0.03 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (32, 7, 2, CAST(0.25 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (33, 7, 25, CAST(0.25 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (34, 7, 11, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (35, 7, 7, CAST(0.03 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (36, 8, 3, CAST(0.10 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (37, 8, 36, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (38, 8, 26, CAST(0.50 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (39, 8, 14, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (40, 9, 4, CAST(0.08 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (41, 9, 37, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (42, 9, 6, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (43, 9, 7, CAST(0.03 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (44, 10, 6, CAST(0.10 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (45, 10, 11, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (46, 10, 10, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (47, 10, 12, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (48, 11, 33, CAST(0.10 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (49, 11, 32, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (50, 11, 16, CAST(0.03 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (51, 12, 26, CAST(2.00 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (52, 12, 30, CAST(0.10 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (53, 12, 16, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (54, 13, 32, CAST(0.08 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (55, 13, 33, CAST(0.12 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (56, 13, 16, CAST(0.04 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (57, 13, 34, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (58, 14, 27, CAST(0.15 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (59, 14, 16, CAST(0.02 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (60, 15, 31, CAST(0.01 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (61, 15, 16, CAST(0.01 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (62, 16, 29, CAST(0.02 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (63, 16, 30, CAST(0.05 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (64, 16, 16, CAST(0.02 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (65, 17, 28, CAST(0.20 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (66, 17, 30, CAST(0.08 AS Decimal(18, 2)))
INSERT [dbo].[DishIngredients] ([DishIngredientID], [DishID], [IngredientID], [QuantityPerDish]) VALUES (67, 17, 16, CAST(0.03 AS Decimal(18, 2)))
SET IDENTITY_INSERT [dbo].[DishIngredients] OFF
GO
SET IDENTITY_INSERT [dbo].[EmployeeRoles] ON 

INSERT [dbo].[EmployeeRoles] ([RoleID], [RoleCode], [RoleName]) VALUES (1, N'ADMIN', N'Quản trị viên')
INSERT [dbo].[EmployeeRoles] ([RoleID], [RoleCode], [RoleName]) VALUES (2, N'MANAGER', N'Quản lý')
INSERT [dbo].[EmployeeRoles] ([RoleID], [RoleCode], [RoleName]) VALUES (3, N'CASHIER', N'Thu ngân')
INSERT [dbo].[EmployeeRoles] ([RoleID], [RoleCode], [RoleName]) VALUES (4, N'WAITER', N'Phục vụ')
INSERT [dbo].[EmployeeRoles] ([RoleID], [RoleCode], [RoleName]) VALUES (5, N'CHEF', N'Đầu bếp')
INSERT [dbo].[EmployeeRoles] ([RoleID], [RoleCode], [RoleName]) VALUES (6, N'KITCHEN_STAFF', N'Nhân viên bếp')
SET IDENTITY_INSERT [dbo].[EmployeeRoles] OFF
GO
SET IDENTITY_INSERT [dbo].[Employees] ON 

INSERT [dbo].[Employees] ([EmployeeID], [Name], [Username], [Password], [Phone], [Email], [Salary], [Shift], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [RoleID]) VALUES (1, N'Admin User', N'admin', N'123456', N'0901111111', N'admin@selfrestaurant.com', CAST(20000000.00 AS Decimal(15, 2)), N'Full-time', 1, CAST(N'2025-11-16T00:22:27.330' AS DateTime), CAST(N'2025-12-03T02:02:51.607' AS DateTime), 1, 1)
INSERT [dbo].[Employees] ([EmployeeID], [Name], [Username], [Password], [Phone], [Email], [Salary], [Shift], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [RoleID]) VALUES (2, N'Quản lý Q1', N'manager_q1', N'123456', N'0902222222', N'manager.q1@selfrestaurant.com', CAST(15000000.00 AS Decimal(15, 2)), N'Full-time', 1, CAST(N'2025-11-16T00:22:27.330' AS DateTime), CAST(N'2025-11-16T00:22:27.330' AS DateTime), 1, 2)
INSERT [dbo].[Employees] ([EmployeeID], [Name], [Username], [Password], [Phone], [Email], [Salary], [Shift], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [RoleID]) VALUES (3, N'Thu ngân Lan', N'cashier_lan', N'123456', N'0903333333', N'lan.cashier@selfrestaurant.com', CAST(8000000.00 AS Decimal(15, 2)), N'Morning', 1, CAST(N'2025-11-16T00:22:27.330' AS DateTime), CAST(N'2025-12-03T01:33:54.293' AS DateTime), 1, 3)
INSERT [dbo].[Employees] ([EmployeeID], [Name], [Username], [Password], [Phone], [Email], [Salary], [Shift], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [RoleID]) VALUES (4, N'Phục vụ Minh', N'waiter_minh', N'123456', N'0904444444', N'minh.waiter@selfrestaurant.com', CAST(7000000.00 AS Decimal(15, 2)), N'Evening', 1, CAST(N'2025-11-16T00:22:27.330' AS DateTime), CAST(N'2025-11-16T00:22:27.330' AS DateTime), 1, 4)
INSERT [dbo].[Employees] ([EmployeeID], [Name], [Username], [Password], [Phone], [Email], [Salary], [Shift], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [RoleID]) VALUES (5, N'Đầu bếp Hùng', N'chef_hung', N'123456', N'0905555555', N'hung.chef@selfrestaurant.com', CAST(12000000.00 AS Decimal(15, 2)), N'Full-time', 1, CAST(N'2025-11-16T00:22:27.330' AS DateTime), CAST(N'2025-12-03T02:08:54.153' AS DateTime), 1, 5)
INSERT [dbo].[Employees] ([EmployeeID], [Name], [Username], [Password], [Phone], [Email], [Salary], [Shift], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [RoleID]) VALUES (6, N'Nguyễn Thu Hà', N'waiter_ha', N'123456', N'0906666666', N'ha.waiter@selfrestaurant.com', CAST(7000000.00 AS Decimal(15, 2)), N'Morning', 1, CAST(N'2025-11-18T14:22:31.603' AS DateTime), CAST(N'2025-11-18T14:22:31.603' AS DateTime), 1, 4)
INSERT [dbo].[Employees] ([EmployeeID], [Name], [Username], [Password], [Phone], [Email], [Salary], [Shift], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [RoleID]) VALUES (7, N'Trần Văn Tú', N'kitchen_tu', N'123456', N'0907777777', N'tu.kitchen@selfrestaurant.com', CAST(8500000.00 AS Decimal(15, 2)), N'Full-time', 1, CAST(N'2025-11-18T14:22:31.603' AS DateTime), CAST(N'2025-11-18T14:22:31.603' AS DateTime), 1, 6)
INSERT [dbo].[Employees] ([EmployeeID], [Name], [Username], [Password], [Phone], [Email], [Salary], [Shift], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [RoleID]) VALUES (8, N'Quản lý GV', N'manager_gv', N'123456', N'0908888888', N'manager.gv@selfrestaurant.com', CAST(15000000.00 AS Decimal(15, 2)), N'Full-time', 1, CAST(N'2025-11-18T14:22:31.603' AS DateTime), CAST(N'2025-11-18T14:22:31.603' AS DateTime), 2, 2)
INSERT [dbo].[Employees] ([EmployeeID], [Name], [Username], [Password], [Phone], [Email], [Salary], [Shift], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [RoleID]) VALUES (9, N'Thu ngân Hương', N'cashier_huong', N'123456', N'0909999999', N'huong.cashier@selfrestaurant.com', CAST(8000000.00 AS Decimal(15, 2)), N'Evening', 1, CAST(N'2025-11-18T14:22:31.603' AS DateTime), CAST(N'2025-11-18T14:22:31.603' AS DateTime), 2, 3)
INSERT [dbo].[Employees] ([EmployeeID], [Name], [Username], [Password], [Phone], [Email], [Salary], [Shift], [IsActive], [CreatedAt], [UpdatedAt], [BranchID], [RoleID]) VALUES (10, N'Đầu bếp Nam', N'chef_nam', N'123456', N'0901010101', N'nam.chef@selfrestaurant.com', CAST(12000000.00 AS Decimal(15, 2)), N'Full-time', 1, CAST(N'2025-11-18T14:22:31.603' AS DateTime), CAST(N'2025-11-18T14:22:31.603' AS DateTime), 2, 5)
SET IDENTITY_INSERT [dbo].[Employees] OFF
GO
SET IDENTITY_INSERT [dbo].[Ingredients] ON 

INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (1, N'Thịt bò', N'kg', CAST(50.00 AS Decimal(18, 2)), CAST(10.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (2, N'Thịt gà', N'kg', CAST(40.00 AS Decimal(18, 2)), CAST(8.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (3, N'Thịt heo', N'kg', CAST(45.00 AS Decimal(18, 2)), CAST(10.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (4, N'Tôm', N'kg', CAST(20.00 AS Decimal(18, 2)), CAST(5.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (5, N'Cá', N'kg', CAST(15.00 AS Decimal(18, 2)), CAST(5.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (6, N'Rau xà lách', N'kg', CAST(10.00 AS Decimal(18, 2)), CAST(2.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (7, N'Rau thơm', N'kg', CAST(8.00 AS Decimal(18, 2)), CAST(2.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (8, N'Hành lá', N'kg', CAST(5.00 AS Decimal(18, 2)), CAST(1.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (9, N'Giá đỗ', N'kg', CAST(10.00 AS Decimal(18, 2)), CAST(2.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (10, N'Cà rót', N'kg', CAST(8.00 AS Decimal(18, 2)), CAST(2.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (11, N'Dưa chuột', N'kg', CAST(10.00 AS Decimal(18, 2)), CAST(2.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (12, N'Bắp cải', N'kg', CAST(12.00 AS Decimal(18, 2)), CAST(3.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (13, N'Nước mắm', N'lít', CAST(20.00 AS Decimal(18, 2)), CAST(5.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (14, N'Dầu ăn', N'lít', CAST(30.00 AS Decimal(18, 2)), CAST(5.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (15, N'Muối', N'kg', CAST(10.00 AS Decimal(18, 2)), CAST(2.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (16, N'Đường', N'kg', CAST(15.00 AS Decimal(18, 2)), CAST(3.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (17, N'Tiêu', N'kg', CAST(2.00 AS Decimal(18, 2)), CAST(0.50 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (18, N'Tỏi', N'kg', CAST(5.00 AS Decimal(18, 2)), CAST(1.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (19, N'Hành tím', N'kg', CAST(5.00 AS Decimal(18, 2)), CAST(1.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (20, N'Sả', N'kg', CAST(3.00 AS Decimal(18, 2)), CAST(1.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (21, N'Bánh phở', N'kg', CAST(50.00 AS Decimal(18, 2)), CAST(10.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (22, N'Bún', N'kg', CAST(40.00 AS Decimal(18, 2)), CAST(10.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (23, N'Mì trứng', N'kg', CAST(30.00 AS Decimal(18, 2)), CAST(8.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (24, N'Hủ tiếu', N'kg', CAST(25.00 AS Decimal(18, 2)), CAST(5.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (25, N'Gạo', N'kg', CAST(100.00 AS Decimal(18, 2)), CAST(20.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (26, N'Trứng gà', N'quả', CAST(200.00 AS Decimal(18, 2)), CAST(50.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (27, N'Cam tươi', N'kg', CAST(20.00 AS Decimal(18, 2)), CAST(5.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (28, N'Bơ', N'kg', CAST(15.00 AS Decimal(18, 2)), CAST(3.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (29, N'Cà phê', N'kg', CAST(10.00 AS Decimal(18, 2)), CAST(2.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (30, N'Sữa đặc', N'lon', CAST(50.00 AS Decimal(18, 2)), CAST(10.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (31, N'Trà', N'kg', CAST(5.00 AS Decimal(18, 2)), CAST(1.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (32, N'Thạch', N'kg', CAST(5.00 AS Decimal(18, 2)), CAST(1.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (33, N'Nước cốt dừa', N'lít', CAST(10.00 AS Decimal(18, 2)), CAST(2.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (34, N'Đậu đỏ', N'kg', CAST(8.00 AS Decimal(18, 2)), CAST(2.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (35, N'Khoai môn', N'kg', CAST(10.00 AS Decimal(18, 2)), CAST(2.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (36, N'Bánh đa nem', N'kg', CAST(5.00 AS Decimal(18, 2)), CAST(1.00 AS Decimal(18, 2)), 1)
INSERT [dbo].[Ingredients] ([IngredientID], [Name], [Unit], [CurrentStock], [ReorderLevel], [IsActive]) VALUES (37, N'Bánh tráng', N'kg', CAST(8.00 AS Decimal(18, 2)), CAST(2.00 AS Decimal(18, 2)), 1)
SET IDENTITY_INSERT [dbo].[Ingredients] OFF
GO
SET IDENTITY_INSERT [dbo].[LoyaltyCards] ON 

INSERT [dbo].[LoyaltyCards] ([CardID], [Points], [IssueDate], [ExpiryDate], [IsActive], [CustomerID]) VALUES (1, 1200, CAST(N'2024-01-01' AS Date), CAST(N'2025-12-31' AS Date), 1, 1)
INSERT [dbo].[LoyaltyCards] ([CardID], [Points], [IssueDate], [ExpiryDate], [IsActive], [CustomerID]) VALUES (2, 850, CAST(N'2024-01-15' AS Date), CAST(N'2025-12-31' AS Date), 1, 2)
INSERT [dbo].[LoyaltyCards] ([CardID], [Points], [IssueDate], [ExpiryDate], [IsActive], [CustomerID]) VALUES (3, 500, CAST(N'2024-02-01' AS Date), CAST(N'2025-12-31' AS Date), 1, 3)
INSERT [dbo].[LoyaltyCards] ([CardID], [Points], [IssueDate], [ExpiryDate], [IsActive], [CustomerID]) VALUES (4, 320, CAST(N'2024-03-10' AS Date), CAST(N'2025-12-31' AS Date), 1, 4)
INSERT [dbo].[LoyaltyCards] ([CardID], [Points], [IssueDate], [ExpiryDate], [IsActive], [CustomerID]) VALUES (5, 680, CAST(N'2024-02-20' AS Date), CAST(N'2025-12-31' AS Date), 1, 5)
INSERT [dbo].[LoyaltyCards] ([CardID], [Points], [IssueDate], [ExpiryDate], [IsActive], [CustomerID]) VALUES (6, 1450, CAST(N'2023-12-15' AS Date), CAST(N'2025-12-31' AS Date), 1, 6)
INSERT [dbo].[LoyaltyCards] ([CardID], [Points], [IssueDate], [ExpiryDate], [IsActive], [CustomerID]) VALUES (7, 280, CAST(N'2024-04-05' AS Date), CAST(N'2025-12-31' AS Date), 1, 7)
INSERT [dbo].[LoyaltyCards] ([CardID], [Points], [IssueDate], [ExpiryDate], [IsActive], [CustomerID]) VALUES (8, 920, CAST(N'2024-01-25' AS Date), CAST(N'2025-12-31' AS Date), 1, 8)
SET IDENTITY_INSERT [dbo].[LoyaltyCards] OFF
GO
SET IDENTITY_INSERT [dbo].[MenuCategory] ON 

INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 1, 1, 1)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (2, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 1, 1, 2)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (3, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 1, 1, 3)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (4, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 1, 1, 4)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (5, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 1, 2, 1)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (6, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 1, 2, 2)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (7, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 1, 2, 4)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (8, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 1, 3, 1)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (9, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 1, 3, 3)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (10, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-16T00:22:27.327' AS DateTime), 1, 3, 4)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (11, CAST(N'2025-11-19T14:22:32.853' AS DateTime), CAST(N'2025-11-19T14:22:32.853' AS DateTime), 1, 4, 1)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (12, CAST(N'2025-11-19T14:22:32.920' AS DateTime), CAST(N'2025-11-19T14:22:32.920' AS DateTime), 1, 4, 2)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (13, CAST(N'2025-11-19T14:22:32.927' AS DateTime), CAST(N'2025-11-19T14:22:32.927' AS DateTime), 1, 4, 3)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (14, CAST(N'2025-11-19T14:22:32.933' AS DateTime), CAST(N'2025-11-19T14:22:32.933' AS DateTime), 1, 4, 4)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (15, CAST(N'2025-11-19T22:56:20.940' AS DateTime), CAST(N'2025-11-19T22:56:20.940' AS DateTime), 1, 5, 1)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (16, CAST(N'2025-11-19T22:56:21.040' AS DateTime), CAST(N'2025-11-19T22:56:21.040' AS DateTime), 1, 5, 2)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (17, CAST(N'2025-11-19T22:56:25.077' AS DateTime), CAST(N'2025-11-19T22:56:25.077' AS DateTime), 1, 5, 3)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (18, CAST(N'2025-11-19T22:56:25.107' AS DateTime), CAST(N'2025-11-19T22:56:25.107' AS DateTime), 1, 5, 4)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (19, CAST(N'2025-11-19T23:00:12.383' AS DateTime), CAST(N'2025-11-19T23:00:12.383' AS DateTime), 1, 6, 1)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (20, CAST(N'2025-11-19T23:00:12.437' AS DateTime), CAST(N'2025-11-19T23:00:12.437' AS DateTime), 1, 6, 2)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (21, CAST(N'2025-11-19T23:00:12.443' AS DateTime), CAST(N'2025-11-19T23:00:12.443' AS DateTime), 1, 6, 3)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (22, CAST(N'2025-11-19T23:00:12.460' AS DateTime), CAST(N'2025-11-19T23:00:12.460' AS DateTime), 1, 6, 4)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (23, CAST(N'2025-11-21T01:28:33.263' AS DateTime), CAST(N'2025-11-21T01:28:33.263' AS DateTime), 1, 7, 1)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (24, CAST(N'2025-11-21T01:28:33.333' AS DateTime), CAST(N'2025-11-21T01:28:33.333' AS DateTime), 1, 7, 2)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (25, CAST(N'2025-11-21T01:28:33.347' AS DateTime), CAST(N'2025-11-21T01:28:33.347' AS DateTime), 1, 7, 3)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (26, CAST(N'2025-11-21T01:28:33.367' AS DateTime), CAST(N'2025-11-21T01:28:33.367' AS DateTime), 1, 7, 4)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (27, CAST(N'2025-11-21T01:51:50.453' AS DateTime), CAST(N'2025-11-21T01:51:50.453' AS DateTime), 1, 8, 1)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (28, CAST(N'2025-11-21T01:51:50.513' AS DateTime), CAST(N'2025-11-21T01:51:50.513' AS DateTime), 1, 8, 2)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (29, CAST(N'2025-11-21T01:51:50.520' AS DateTime), CAST(N'2025-11-21T01:51:50.520' AS DateTime), 1, 8, 3)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (30, CAST(N'2025-11-21T01:51:50.523' AS DateTime), CAST(N'2025-11-21T01:51:50.523' AS DateTime), 1, 8, 4)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (31, CAST(N'2025-11-22T12:59:44.957' AS DateTime), CAST(N'2025-11-22T12:59:44.957' AS DateTime), 1, 9, 1)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (32, CAST(N'2025-11-22T12:59:45.013' AS DateTime), CAST(N'2025-11-22T12:59:45.013' AS DateTime), 1, 9, 2)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (33, CAST(N'2025-11-22T12:59:45.020' AS DateTime), CAST(N'2025-11-22T12:59:45.020' AS DateTime), 1, 9, 3)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (34, CAST(N'2025-11-22T12:59:45.043' AS DateTime), CAST(N'2025-11-22T12:59:45.043' AS DateTime), 1, 9, 4)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (35, CAST(N'2025-11-22T16:18:39.123' AS DateTime), CAST(N'2025-11-22T16:18:39.123' AS DateTime), 1, 10, 1)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (36, CAST(N'2025-11-22T16:18:39.183' AS DateTime), CAST(N'2025-11-22T16:18:39.183' AS DateTime), 1, 10, 2)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (37, CAST(N'2025-11-22T16:18:39.197' AS DateTime), CAST(N'2025-11-22T16:18:39.197' AS DateTime), 1, 10, 3)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (38, CAST(N'2025-11-22T16:18:39.200' AS DateTime), CAST(N'2025-11-22T16:18:39.200' AS DateTime), 1, 10, 4)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (39, CAST(N'2025-11-30T13:20:21.313' AS DateTime), CAST(N'2025-11-30T13:20:21.313' AS DateTime), 1, 11, 1)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (40, CAST(N'2025-11-30T13:20:21.377' AS DateTime), CAST(N'2025-11-30T13:20:21.377' AS DateTime), 1, 11, 2)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (41, CAST(N'2025-11-30T13:20:21.383' AS DateTime), CAST(N'2025-11-30T13:20:21.383' AS DateTime), 1, 11, 3)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (42, CAST(N'2025-11-30T13:20:21.390' AS DateTime), CAST(N'2025-11-30T13:20:21.390' AS DateTime), 1, 11, 4)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (43, CAST(N'2025-12-01T11:30:40.540' AS DateTime), CAST(N'2025-12-01T11:30:40.540' AS DateTime), 1, 12, 1)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (44, CAST(N'2025-12-01T11:30:40.600' AS DateTime), CAST(N'2025-12-01T11:30:40.600' AS DateTime), 1, 12, 2)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (45, CAST(N'2025-12-01T11:30:40.607' AS DateTime), CAST(N'2025-12-01T11:30:40.607' AS DateTime), 1, 12, 3)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (46, CAST(N'2025-12-01T11:30:40.613' AS DateTime), CAST(N'2025-12-01T11:30:40.613' AS DateTime), 1, 12, 4)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (47, CAST(N'2025-12-02T01:03:59.353' AS DateTime), CAST(N'2025-12-02T01:03:59.353' AS DateTime), 1, 13, 1)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (48, CAST(N'2025-12-02T01:03:59.420' AS DateTime), CAST(N'2025-12-02T01:03:59.420' AS DateTime), 1, 13, 2)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (49, CAST(N'2025-12-02T01:03:59.440' AS DateTime), CAST(N'2025-12-02T01:03:59.440' AS DateTime), 1, 13, 3)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (50, CAST(N'2025-12-02T01:03:59.463' AS DateTime), CAST(N'2025-12-02T01:03:59.463' AS DateTime), 1, 13, 4)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (51, CAST(N'2025-12-03T00:21:59.997' AS DateTime), CAST(N'2025-12-03T00:21:59.997' AS DateTime), 1, 14, 1)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (52, CAST(N'2025-12-03T00:22:00.100' AS DateTime), CAST(N'2025-12-03T00:22:00.100' AS DateTime), 1, 14, 2)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (53, CAST(N'2025-12-03T00:22:00.113' AS DateTime), CAST(N'2025-12-03T00:22:00.113' AS DateTime), 1, 14, 3)
INSERT [dbo].[MenuCategory] ([MenuCategoryID], [CreatedAt], [UpdatedAt], [IsActive], [MenuID], [CategoryID]) VALUES (54, CAST(N'2025-12-03T00:22:00.120' AS DateTime), CAST(N'2025-12-03T00:22:00.120' AS DateTime), 1, 14, 4)
SET IDENTITY_INSERT [dbo].[MenuCategory] OFF
GO
SET IDENTITY_INSERT [dbo].[Menus] ON 

INSERT [dbo].[Menus] ([MenuID], [MenuName], [Date], [IsActive], [CreatedAt], [UpdatedAt], [BranchID]) VALUES (1, N'Menu Quận 1 - 18/11/2025', CAST(N'2025-11-18' AS Date), 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-18T14:40:22.713' AS DateTime), 1)
INSERT [dbo].[Menus] ([MenuID], [MenuName], [Date], [IsActive], [CreatedAt], [UpdatedAt], [BranchID]) VALUES (2, N'Menu Gò Vấp - 18/11/2025', CAST(N'2025-11-18' AS Date), 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-18T14:40:22.713' AS DateTime), 2)
INSERT [dbo].[Menus] ([MenuID], [MenuName], [Date], [IsActive], [CreatedAt], [UpdatedAt], [BranchID]) VALUES (3, N'Menu Bình Thạnh - 18/11/2025', CAST(N'2025-11-18' AS Date), 1, CAST(N'2025-11-16T00:22:27.327' AS DateTime), CAST(N'2025-11-18T14:40:22.713' AS DateTime), 3)
INSERT [dbo].[Menus] ([MenuID], [MenuName], [Date], [IsActive], [CreatedAt], [UpdatedAt], [BranchID]) VALUES (4, N'Menu Chi nhánh Qu?n 1 - 19/11/2025', CAST(N'2025-11-19' AS Date), 1, CAST(N'2025-11-19T14:22:32.797' AS DateTime), CAST(N'2025-11-19T14:22:32.797' AS DateTime), 1)
INSERT [dbo].[Menus] ([MenuID], [MenuName], [Date], [IsActive], [CreatedAt], [UpdatedAt], [BranchID]) VALUES (5, N'Menu Chi nhánh Bình Th?nh - 19/11/2025', CAST(N'2025-11-19' AS Date), 1, CAST(N'2025-11-19T22:56:20.860' AS DateTime), CAST(N'2025-11-19T22:56:20.860' AS DateTime), 3)
INSERT [dbo].[Menus] ([MenuID], [MenuName], [Date], [IsActive], [CreatedAt], [UpdatedAt], [BranchID]) VALUES (6, N'Menu Chi nhánh Gò V?p - 19/11/2025', CAST(N'2025-11-19' AS Date), 1, CAST(N'2025-11-19T23:00:12.320' AS DateTime), CAST(N'2025-11-19T23:00:12.320' AS DateTime), 2)
INSERT [dbo].[Menus] ([MenuID], [MenuName], [Date], [IsActive], [CreatedAt], [UpdatedAt], [BranchID]) VALUES (7, N'Menu Chi nhánh Bình Th?nh - 21/11/2025', CAST(N'2025-11-21' AS Date), 1, CAST(N'2025-11-21T01:28:33.183' AS DateTime), CAST(N'2025-11-21T01:28:33.183' AS DateTime), 3)
INSERT [dbo].[Menus] ([MenuID], [MenuName], [Date], [IsActive], [CreatedAt], [UpdatedAt], [BranchID]) VALUES (8, N'Menu Chi nhánh Gò V?p - 21/11/2025', CAST(N'2025-11-21' AS Date), 1, CAST(N'2025-11-21T01:51:50.393' AS DateTime), CAST(N'2025-11-21T01:51:50.393' AS DateTime), 2)
INSERT [dbo].[Menus] ([MenuID], [MenuName], [Date], [IsActive], [CreatedAt], [UpdatedAt], [BranchID]) VALUES (9, N'Menu Chi nhánh Quận 1 - 22/11/2025', CAST(N'2025-11-22' AS Date), 1, CAST(N'2025-11-22T12:59:44.870' AS DateTime), CAST(N'2025-11-22T12:59:44.870' AS DateTime), 1)
INSERT [dbo].[Menus] ([MenuID], [MenuName], [Date], [IsActive], [CreatedAt], [UpdatedAt], [BranchID]) VALUES (10, N'Menu Chi nhánh Bình Thạnh - 22/11/2025', CAST(N'2025-11-22' AS Date), 1, CAST(N'2025-11-22T16:18:39.037' AS DateTime), CAST(N'2025-11-22T16:18:39.037' AS DateTime), 3)
INSERT [dbo].[Menus] ([MenuID], [MenuName], [Date], [IsActive], [CreatedAt], [UpdatedAt], [BranchID]) VALUES (11, N'Menu Chi nhánh Quận 1 - 30/11/2025', CAST(N'2025-11-30' AS Date), 1, CAST(N'2025-11-30T13:20:21.233' AS DateTime), CAST(N'2025-11-30T13:20:21.233' AS DateTime), 1)
INSERT [dbo].[Menus] ([MenuID], [MenuName], [Date], [IsActive], [CreatedAt], [UpdatedAt], [BranchID]) VALUES (12, N'Menu Chi nhánh Quận 1 - 01/12/2025', CAST(N'2025-12-01' AS Date), 1, CAST(N'2025-12-01T11:30:40.460' AS DateTime), CAST(N'2025-12-01T11:30:40.460' AS DateTime), 1)
INSERT [dbo].[Menus] ([MenuID], [MenuName], [Date], [IsActive], [CreatedAt], [UpdatedAt], [BranchID]) VALUES (13, N'Menu Chi nhánh Quận 1 - 02/12/2025', CAST(N'2025-12-02' AS Date), 1, CAST(N'2025-12-02T01:03:59.257' AS DateTime), CAST(N'2025-12-02T01:03:59.257' AS DateTime), 1)
INSERT [dbo].[Menus] ([MenuID], [MenuName], [Date], [IsActive], [CreatedAt], [UpdatedAt], [BranchID]) VALUES (14, N'Menu Chi nhánh Quận 1 - 03/12/2025', CAST(N'2025-12-03' AS Date), 1, CAST(N'2025-12-03T00:21:59.907' AS DateTime), CAST(N'2025-12-03T00:21:59.907' AS DateTime), 1)
SET IDENTITY_INSERT [dbo].[Menus] OFF
GO
SET IDENTITY_INSERT [dbo].[OrderItemIngredients] ON 

INSERT [dbo].[OrderItemIngredients] ([OrderItemIngredientID], [OrderItemID], [IngredientID], [Quantity], [IsRemoved], [CreatedAt], [UpdatedAt]) VALUES (1, 78, 29, CAST(0.020 AS Decimal(18, 3)), 0, CAST(N'2025-12-03T01:38:02.917' AS DateTime), CAST(N'2025-12-03T01:38:02.920' AS DateTime))
INSERT [dbo].[OrderItemIngredients] ([OrderItemIngredientID], [OrderItemID], [IngredientID], [Quantity], [IsRemoved], [CreatedAt], [UpdatedAt]) VALUES (2, 78, 30, CAST(0.050 AS Decimal(18, 3)), 0, CAST(N'2025-12-03T01:38:02.933' AS DateTime), CAST(N'2025-12-03T01:38:02.933' AS DateTime))
INSERT [dbo].[OrderItemIngredients] ([OrderItemIngredientID], [OrderItemID], [IngredientID], [Quantity], [IsRemoved], [CreatedAt], [UpdatedAt]) VALUES (3, 78, 16, CAST(0.020 AS Decimal(18, 3)), 1, CAST(N'2025-12-03T01:38:02.933' AS DateTime), CAST(N'2025-12-03T01:38:02.933' AS DateTime))
SET IDENTITY_INSERT [dbo].[OrderItemIngredients] OFF
GO
SET IDENTITY_INSERT [dbo].[OrderItems] ON 

INSERT [dbo].[OrderItems] ([ItemID], [Quantity], [UnitPrice], [LineTotal], [Note], [OrderID], [DishID]) VALUES (78, 1, CAST(40000.00 AS Decimal(15, 2)), CAST(40000.00 AS Decimal(15, 2)), N'Không đường', 30, 16)
SET IDENTITY_INSERT [dbo].[OrderItems] OFF
GO
SET IDENTITY_INSERT [dbo].[OrderStatus] ON 

INSERT [dbo].[OrderStatus] ([StatusID], [StatusCode], [StatusName]) VALUES (1, N'PENDING', N'Chờ xử lý')
INSERT [dbo].[OrderStatus] ([StatusID], [StatusCode], [StatusName]) VALUES (2, N'CONFIRMED', N'Đã xác nhận')
INSERT [dbo].[OrderStatus] ([StatusID], [StatusCode], [StatusName]) VALUES (3, N'PREPARING', N'Đang chuẩn bị')
INSERT [dbo].[OrderStatus] ([StatusID], [StatusCode], [StatusName]) VALUES (4, N'READY', N'Sẵn sàng')
INSERT [dbo].[OrderStatus] ([StatusID], [StatusCode], [StatusName]) VALUES (5, N'SERVING', N'Đang phục vụ')
INSERT [dbo].[OrderStatus] ([StatusID], [StatusCode], [StatusName]) VALUES (6, N'COMPLETED', N'Hoàn thành')
INSERT [dbo].[OrderStatus] ([StatusID], [StatusCode], [StatusName]) VALUES (7, N'CANCELLED', N'Đã hủy')
SET IDENTITY_INSERT [dbo].[OrderStatus] OFF
GO
SET IDENTITY_INSERT [dbo].[PasswordResetTokens] ON 

INSERT [dbo].[PasswordResetTokens] ([TokenID], [CustomerID], [Token], [ExpiryDate], [IsUsed], [CreatedAt]) VALUES (1, 9, N'D16WbOq4LBmcb_Xeytug-OZ9aI253vpT9JcESXVr_KY', CAST(N'2025-11-30T14:42:11.777' AS DateTime), 1, CAST(N'2025-11-30T14:12:11.777' AS DateTime))
INSERT [dbo].[PasswordResetTokens] ([TokenID], [CustomerID], [Token], [ExpiryDate], [IsUsed], [CreatedAt]) VALUES (2, 9, N'N6fvMkSJBfy3nDAVpfxT6P0TDBRsaX1k3_4gIFyLXjQ', CAST(N'2025-11-30T14:45:24.070' AS DateTime), 0, CAST(N'2025-11-30T14:15:24.070' AS DateTime))
INSERT [dbo].[PasswordResetTokens] ([TokenID], [CustomerID], [Token], [ExpiryDate], [IsUsed], [CreatedAt]) VALUES (3, 9, N'W4EXpTVp9eXvTgWsYTv0w9Dsv0G8_7FeFD_-mK2HwUM', CAST(N'2025-11-30T14:47:07.797' AS DateTime), 0, CAST(N'2025-11-30T14:17:07.797' AS DateTime))
INSERT [dbo].[PasswordResetTokens] ([TokenID], [CustomerID], [Token], [ExpiryDate], [IsUsed], [CreatedAt]) VALUES (4, 9, N'62zz7OVTt35PTc3bLZMyW-jOXbPYCPckEtJH1fIifu8', CAST(N'2025-11-30T14:54:33.383' AS DateTime), 0, CAST(N'2025-11-30T14:24:33.383' AS DateTime))
INSERT [dbo].[PasswordResetTokens] ([TokenID], [CustomerID], [Token], [ExpiryDate], [IsUsed], [CreatedAt]) VALUES (5, 9, N'psF7oO2942EUvf54gopYb1D6YHxqplWcCCGkcW0WnH8', CAST(N'2025-11-30T14:57:19.077' AS DateTime), 0, CAST(N'2025-11-30T14:27:19.077' AS DateTime))
SET IDENTITY_INSERT [dbo].[PasswordResetTokens] OFF
GO
SET IDENTITY_INSERT [dbo].[PaymentMethod] ON 

INSERT [dbo].[PaymentMethod] ([MethodID], [MethodCode], [MethodName]) VALUES (1, N'CASH', N'Tiền mặt')
INSERT [dbo].[PaymentMethod] ([MethodID], [MethodCode], [MethodName]) VALUES (2, N'CARD', N'Thẻ tín dụng/ghi nợ')
INSERT [dbo].[PaymentMethod] ([MethodID], [MethodCode], [MethodName]) VALUES (3, N'MOMO', N'Ví MoMo')
INSERT [dbo].[PaymentMethod] ([MethodID], [MethodCode], [MethodName]) VALUES (4, N'ZALOPAY', N'ZaloPay')
INSERT [dbo].[PaymentMethod] ([MethodID], [MethodCode], [MethodName]) VALUES (5, N'VNPAY', N'VNPay')
INSERT [dbo].[PaymentMethod] ([MethodID], [MethodCode], [MethodName]) VALUES (6, N'BANKING', N'Chuyển khoản ngân hàng')
SET IDENTITY_INSERT [dbo].[PaymentMethod] OFF
GO
SET IDENTITY_INSERT [dbo].[PaymentStatus] ON 

INSERT [dbo].[PaymentStatus] ([StatusID], [StatusCode], [StatusName]) VALUES (1, N'PENDING', N'Chờ thanh toán')
INSERT [dbo].[PaymentStatus] ([StatusID], [StatusCode], [StatusName]) VALUES (2, N'COMPLETED', N'Đã thanh toán')
INSERT [dbo].[PaymentStatus] ([StatusID], [StatusCode], [StatusName]) VALUES (3, N'FAILED', N'Thanh toán thất bại')
INSERT [dbo].[PaymentStatus] ([StatusID], [StatusCode], [StatusName]) VALUES (4, N'REFUNDED', N'Đã hoàn tiền')
SET IDENTITY_INSERT [dbo].[PaymentStatus] OFF
GO
SET IDENTITY_INSERT [dbo].[Reports] ON 

INSERT [dbo].[Reports] ([ReportID], [ReportType], [GeneratedDate], [Content], [FilePath]) VALUES (1, N'Doanh thu ngày', CAST(N'2025-11-18T14:22:31.620' AS DateTime), N'Tổng doanh thu hôm nay: 1,059,500 VNĐ. Chi nhánh Quận 1 đóng góp nhiều nhất.', N'/reports/daily_20251118.pdf')
INSERT [dbo].[Reports] ([ReportID], [ReportType], [GeneratedDate], [Content], [FilePath]) VALUES (2, N'Món ăn bán chạy', CAST(N'2025-11-18T14:22:31.620' AS DateTime), N'Top 3 món bán chạy: 1) Phở Bò (8 phần), 2) Cơm Sườn (6 phần), 3) Bún Chả (5 phần)', N'/reports/bestsellers_20251118.pdf')
INSERT [dbo].[Reports] ([ReportID], [ReportType], [GeneratedDate], [Content], [FilePath]) VALUES (3, N'Báo cáo nhân viên', CAST(N'2025-11-18T14:22:31.620' AS DateTime), N'Tổng số nhân viên: 10. Tất cả đang hoạt động bình thường.', N'/reports/staff_20251118.pdf')
SET IDENTITY_INSERT [dbo].[Reports] OFF
GO
SET IDENTITY_INSERT [dbo].[Restaurants] ON 

INSERT [dbo].[Restaurants] ([RestaurantID], [Name], [Address], [Description], [Phone], [Email], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (1, N'Self Restaurant', N'Thành phố Hồ Chí Minh', N'Chuỗi nhà hàng tự phục vụ chất lượng cao', N'0281234567', N'info@selfrestaurant.com', 1, CAST(N'2025-11-16T00:22:27.323' AS DateTime), CAST(N'2025-11-16T00:22:27.323' AS DateTime))
SET IDENTITY_INSERT [dbo].[Restaurants] OFF
GO
SET IDENTITY_INSERT [dbo].[TableStatus] ON 

INSERT [dbo].[TableStatus] ([StatusID], [StatusCode], [StatusName]) VALUES (1, N'AVAILABLE', N'Có sẵn')
INSERT [dbo].[TableStatus] ([StatusID], [StatusCode], [StatusName]) VALUES (2, N'OCCUPIED', N'Đang sử dụng')
INSERT [dbo].[TableStatus] ([StatusID], [StatusCode], [StatusName]) VALUES (3, N'RESERVED', N'Đã đặt trước')
INSERT [dbo].[TableStatus] ([StatusID], [StatusCode], [StatusName]) VALUES (4, N'CLEANING', N'Đang dọn dẹp')
INSERT [dbo].[TableStatus] ([StatusID], [StatusCode], [StatusName]) VALUES (5, N'MAINTENANCE', N'Bảo trì')
SET IDENTITY_INSERT [dbo].[TableStatus] OFF
GO
/****** Object:  Index [IX_Bills_BillTime]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [IX_Bills_BillTime] ON [dbo].[Bills]
(
	[BillTime] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Bills_OrderID]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [IX_Bills_OrderID] ON [dbo].[Bills]
(
	[OrderID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_branches_active]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_branches_active] ON [dbo].[Branches]
(
	[IsActive] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_branches_restaurant]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_branches_restaurant] ON [dbo].[Branches]
(
	[RestaurantID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_categories_active]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_categories_active] ON [dbo].[Categories]
(
	[IsActive] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_categories_display]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_categories_display] ON [dbo].[Categories]
(
	[DisplayOrder] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [idx_categories_name]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_categories_name] ON [dbo].[Categories]
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_categorydish_dish]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_categorydish_dish] ON [dbo].[CategoryDish]
(
	[DishID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_categorydish_display]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_categorydish_display] ON [dbo].[CategoryDish]
(
	[DisplayOrder] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_categorydish_menucategory]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_categorydish_menucategory] ON [dbo].[CategoryDish]
(
	[MenuCategoryID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ_Customers_Username]    Script Date: 03/12/2025 2:23:11 SA ******/
ALTER TABLE [dbo].[Customers] ADD  CONSTRAINT [UQ_Customers_Username] UNIQUE NONCLUSTERED 
(
	[Username] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_customers_active]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_customers_active] ON [dbo].[Customers]
(
	[IsActive] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [idx_customers_email]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_customers_email] ON [dbo].[Customers]
(
	[Email] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [idx_customers_phone]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_customers_phone] ON [dbo].[Customers]
(
	[PhoneNumber] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [idx_customers_username]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_customers_username] ON [dbo].[Customers]
(
	[Username] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Customer_Username]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_Customer_Username] ON [dbo].[Customers]
(
	[Username] ASC
)
WHERE ([Username] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_diningtables_branch]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_diningtables_branch] ON [dbo].[DiningTables]
(
	[BranchID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_diningtables_status]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_diningtables_status] ON [dbo].[DiningTables]
(
	[StatusID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_dishes_active]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_dishes_active] ON [dbo].[Dishes]
(
	[IsActive] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_dishes_available]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_dishes_available] ON [dbo].[Dishes]
(
	[Available] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_dishes_category]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_dishes_category] ON [dbo].[Dishes]
(
	[CategoryID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [idx_dishes_name]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_dishes_name] ON [dbo].[Dishes]
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__Employee__D62CB59C25E8707A]    Script Date: 03/12/2025 2:23:11 SA ******/
ALTER TABLE [dbo].[EmployeeRoles] ADD UNIQUE NONCLUSTERED 
(
	[RoleCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__Employee__536C85E46A8B3547]    Script Date: 03/12/2025 2:23:11 SA ******/
ALTER TABLE [dbo].[Employees] ADD UNIQUE NONCLUSTERED 
(
	[Username] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_employees_active]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_employees_active] ON [dbo].[Employees]
(
	[IsActive] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_employees_branch]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_employees_branch] ON [dbo].[Employees]
(
	[BranchID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_employees_role]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_employees_role] ON [dbo].[Employees]
(
	[RoleID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [idx_employees_username]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_employees_username] ON [dbo].[Employees]
(
	[Username] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_loyaltycards_active]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_loyaltycards_active] ON [dbo].[LoyaltyCards]
(
	[IsActive] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_loyaltycards_customer]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_loyaltycards_customer] ON [dbo].[LoyaltyCards]
(
	[CustomerID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [unique_menu_category]    Script Date: 03/12/2025 2:23:11 SA ******/
ALTER TABLE [dbo].[MenuCategory] ADD  CONSTRAINT [unique_menu_category] UNIQUE NONCLUSTERED 
(
	[MenuID] ASC,
	[CategoryID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_menucategory_category]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_menucategory_category] ON [dbo].[MenuCategory]
(
	[CategoryID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_menucategory_menu]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_menucategory_menu] ON [dbo].[MenuCategory]
(
	[MenuID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_menus_active]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_menus_active] ON [dbo].[Menus]
(
	[IsActive] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_menus_branch]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_menus_branch] ON [dbo].[Menus]
(
	[BranchID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_menus_date]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_menus_date] ON [dbo].[Menus]
(
	[Date] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_OrderItemIngredients_OrderItem_Ingredient]    Script Date: 03/12/2025 2:23:11 SA ******/
ALTER TABLE [dbo].[OrderItemIngredients] ADD  CONSTRAINT [UQ_OrderItemIngredients_OrderItem_Ingredient] UNIQUE NONCLUSTERED 
(
	[OrderItemID] ASC,
	[IngredientID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_OrderItemIngredients_OrderItemID]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [IX_OrderItemIngredients_OrderItemID] ON [dbo].[OrderItemIngredients]
(
	[OrderItemID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_orderitems_dish]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_orderitems_dish] ON [dbo].[OrderItems]
(
	[DishID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_orderitems_order]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_orderitems_order] ON [dbo].[OrderItems]
(
	[OrderID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__Orders__999B52291ED657E9]    Script Date: 03/12/2025 2:23:11 SA ******/
ALTER TABLE [dbo].[Orders] ADD UNIQUE NONCLUSTERED 
(
	[OrderCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [idx_orders_code]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_orders_code] ON [dbo].[Orders]
(
	[OrderCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_orders_customer]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_orders_customer] ON [dbo].[Orders]
(
	[CustomerID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_orders_status]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_orders_status] ON [dbo].[Orders]
(
	[StatusID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_orders_table]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_orders_table] ON [dbo].[Orders]
(
	[TableID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_orders_time]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_orders_time] ON [dbo].[Orders]
(
	[OrderTime] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__OrderSta__6A7B44FC0D92A760]    Script Date: 03/12/2025 2:23:11 SA ******/
ALTER TABLE [dbo].[OrderStatus] ADD UNIQUE NONCLUSTERED 
(
	[StatusCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_customer]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_customer] ON [dbo].[PasswordResetTokens]
(
	[CustomerID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [idx_token]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_token] ON [dbo].[PasswordResetTokens]
(
	[Token] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__PaymentM__11E9210D5D748E8C]    Script Date: 03/12/2025 2:23:11 SA ******/
ALTER TABLE [dbo].[PaymentMethod] ADD UNIQUE NONCLUSTERED 
(
	[MethodCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_payments_customer]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_payments_customer] ON [dbo].[Payments]
(
	[CustomerID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_payments_date]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_payments_date] ON [dbo].[Payments]
(
	[Date] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_payments_order]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_payments_order] ON [dbo].[Payments]
(
	[OrderID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_payments_status]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_payments_status] ON [dbo].[Payments]
(
	[StatusID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__PaymentS__6A7B44FC4CF1A484]    Script Date: 03/12/2025 2:23:11 SA ******/
ALTER TABLE [dbo].[PaymentStatus] ADD UNIQUE NONCLUSTERED 
(
	[StatusCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_reports_date]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_reports_date] ON [dbo].[Reports]
(
	[GeneratedDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [idx_reports_type]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_reports_type] ON [dbo].[Reports]
(
	[ReportType] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [idx_restaurants_active]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_restaurants_active] ON [dbo].[Restaurants]
(
	[IsActive] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [idx_restaurants_name]    Script Date: 03/12/2025 2:23:11 SA ******/
CREATE NONCLUSTERED INDEX [idx_restaurants_name] ON [dbo].[Restaurants]
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__TableSta__6A7B44FC3E67C934]    Script Date: 03/12/2025 2:23:11 SA ******/
ALTER TABLE [dbo].[TableStatus] ADD UNIQUE NONCLUSTERED 
(
	[StatusCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[Bills] ADD  DEFAULT (getdate()) FOR [BillTime]
GO
ALTER TABLE [dbo].[Bills] ADD  DEFAULT ((0)) FOR [Discount]
GO
ALTER TABLE [dbo].[Bills] ADD  DEFAULT ((0)) FOR [PointsDiscount]
GO
ALTER TABLE [dbo].[Bills] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Branches] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Branches] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Branches] ADD  DEFAULT (getdate()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[Categories] ADD  DEFAULT ((0)) FOR [DisplayOrder]
GO
ALTER TABLE [dbo].[Categories] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Categories] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Categories] ADD  DEFAULT (getdate()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[CategoryDish] ADD  DEFAULT ((0)) FOR [DisplayOrder]
GO
ALTER TABLE [dbo].[CategoryDish] ADD  DEFAULT ((1)) FOR [IsAvailable]
GO
ALTER TABLE [dbo].[CategoryDish] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[CategoryDish] ADD  DEFAULT (getdate()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[Customers] ADD  DEFAULT ((0)) FOR [LoyaltyPoints]
GO
ALTER TABLE [dbo].[Customers] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Customers] ADD  DEFAULT (getdate()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[Customers] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Customers] ADD  DEFAULT ((0)) FOR [CreditPoints]
GO
ALTER TABLE [dbo].[DiningTables] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[DiningTables] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[DiningTables] ADD  DEFAULT (getdate()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[Dishes] ADD  DEFAULT ((1)) FOR [Available]
GO
ALTER TABLE [dbo].[Dishes] ADD  DEFAULT ((0)) FOR [IsVegetarian]
GO
ALTER TABLE [dbo].[Dishes] ADD  DEFAULT ((0)) FOR [IsDailySpecial]
GO
ALTER TABLE [dbo].[Dishes] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Dishes] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Dishes] ADD  DEFAULT (getdate()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[Employees] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Employees] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Employees] ADD  DEFAULT (getdate()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[Ingredients] ADD  DEFAULT ((0)) FOR [CurrentStock]
GO
ALTER TABLE [dbo].[Ingredients] ADD  DEFAULT ((0)) FOR [ReorderLevel]
GO
ALTER TABLE [dbo].[Ingredients] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[LoyaltyCards] ADD  DEFAULT ((0)) FOR [Points]
GO
ALTER TABLE [dbo].[LoyaltyCards] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[MenuCategory] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[MenuCategory] ADD  DEFAULT (getdate()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[MenuCategory] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Menus] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Menus] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Menus] ADD  DEFAULT (getdate()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[OrderItemIngredients] ADD  DEFAULT ((0)) FOR [IsRemoved]
GO
ALTER TABLE [dbo].[OrderItemIngredients] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[OrderItems] ADD  DEFAULT ((1)) FOR [Quantity]
GO
ALTER TABLE [dbo].[Orders] ADD  DEFAULT (getdate()) FOR [OrderTime]
GO
ALTER TABLE [dbo].[Orders] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[PasswordResetTokens] ADD  DEFAULT ((0)) FOR [IsUsed]
GO
ALTER TABLE [dbo].[PasswordResetTokens] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Payments] ADD  DEFAULT (getdate()) FOR [Date]
GO
ALTER TABLE [dbo].[Reports] ADD  DEFAULT (getdate()) FOR [GeneratedDate]
GO
ALTER TABLE [dbo].[Restaurants] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Restaurants] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Restaurants] ADD  DEFAULT (getdate()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[Bills]  WITH CHECK ADD  CONSTRAINT [FK_Bills_Customers] FOREIGN KEY([CustomerID])
REFERENCES [dbo].[Customers] ([CustomerID])
GO
ALTER TABLE [dbo].[Bills] CHECK CONSTRAINT [FK_Bills_Customers]
GO
ALTER TABLE [dbo].[Bills]  WITH CHECK ADD  CONSTRAINT [FK_Bills_Employees] FOREIGN KEY([EmployeeID])
REFERENCES [dbo].[Employees] ([EmployeeID])
GO
ALTER TABLE [dbo].[Bills] CHECK CONSTRAINT [FK_Bills_Employees]
GO
ALTER TABLE [dbo].[Bills]  WITH CHECK ADD  CONSTRAINT [FK_Bills_Orders] FOREIGN KEY([OrderID])
REFERENCES [dbo].[Orders] ([OrderID])
GO
ALTER TABLE [dbo].[Bills] CHECK CONSTRAINT [FK_Bills_Orders]
GO
ALTER TABLE [dbo].[Branches]  WITH CHECK ADD FOREIGN KEY([RestaurantID])
REFERENCES [dbo].[Restaurants] ([RestaurantID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[CategoryDish]  WITH CHECK ADD FOREIGN KEY([DishID])
REFERENCES [dbo].[Dishes] ([DishID])
GO
ALTER TABLE [dbo].[CategoryDish]  WITH CHECK ADD FOREIGN KEY([MenuCategoryID])
REFERENCES [dbo].[MenuCategory] ([MenuCategoryID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[DiningTables]  WITH CHECK ADD FOREIGN KEY([BranchID])
REFERENCES [dbo].[Branches] ([BranchID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[DiningTables]  WITH CHECK ADD FOREIGN KEY([StatusID])
REFERENCES [dbo].[TableStatus] ([StatusID])
GO
ALTER TABLE [dbo].[Dishes]  WITH CHECK ADD FOREIGN KEY([CategoryID])
REFERENCES [dbo].[Categories] ([CategoryID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[DishIngredients]  WITH CHECK ADD  CONSTRAINT [FK_DishIngredients_Dishes] FOREIGN KEY([DishID])
REFERENCES [dbo].[Dishes] ([DishID])
GO
ALTER TABLE [dbo].[DishIngredients] CHECK CONSTRAINT [FK_DishIngredients_Dishes]
GO
ALTER TABLE [dbo].[DishIngredients]  WITH CHECK ADD  CONSTRAINT [FK_DishIngredients_Ingredients] FOREIGN KEY([IngredientID])
REFERENCES [dbo].[Ingredients] ([IngredientID])
GO
ALTER TABLE [dbo].[DishIngredients] CHECK CONSTRAINT [FK_DishIngredients_Ingredients]
GO
ALTER TABLE [dbo].[Employees]  WITH CHECK ADD FOREIGN KEY([BranchID])
REFERENCES [dbo].[Branches] ([BranchID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Employees]  WITH CHECK ADD FOREIGN KEY([RoleID])
REFERENCES [dbo].[EmployeeRoles] ([RoleID])
GO
ALTER TABLE [dbo].[LoyaltyCards]  WITH CHECK ADD FOREIGN KEY([CustomerID])
REFERENCES [dbo].[Customers] ([CustomerID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[MenuCategory]  WITH CHECK ADD FOREIGN KEY([CategoryID])
REFERENCES [dbo].[Categories] ([CategoryID])
GO
ALTER TABLE [dbo].[MenuCategory]  WITH CHECK ADD FOREIGN KEY([MenuID])
REFERENCES [dbo].[Menus] ([MenuID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Menus]  WITH CHECK ADD FOREIGN KEY([BranchID])
REFERENCES [dbo].[Branches] ([BranchID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[OrderItemIngredients]  WITH CHECK ADD  CONSTRAINT [FK_OrderItemIngredients_Ingredients] FOREIGN KEY([IngredientID])
REFERENCES [dbo].[Ingredients] ([IngredientID])
GO
ALTER TABLE [dbo].[OrderItemIngredients] CHECK CONSTRAINT [FK_OrderItemIngredients_Ingredients]
GO
ALTER TABLE [dbo].[OrderItemIngredients]  WITH CHECK ADD  CONSTRAINT [FK_OrderItemIngredients_OrderItems] FOREIGN KEY([OrderItemID])
REFERENCES [dbo].[OrderItems] ([ItemID])
GO
ALTER TABLE [dbo].[OrderItemIngredients] CHECK CONSTRAINT [FK_OrderItemIngredients_OrderItems]
GO
ALTER TABLE [dbo].[OrderItems]  WITH CHECK ADD FOREIGN KEY([DishID])
REFERENCES [dbo].[Dishes] ([DishID])
GO
ALTER TABLE [dbo].[Orders]  WITH CHECK ADD FOREIGN KEY([CustomerID])
REFERENCES [dbo].[Customers] ([CustomerID])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Orders]  WITH CHECK ADD FOREIGN KEY([StatusID])
REFERENCES [dbo].[OrderStatus] ([StatusID])
GO
ALTER TABLE [dbo].[Orders]  WITH CHECK ADD FOREIGN KEY([TableID])
REFERENCES [dbo].[DiningTables] ([TableID])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Orders]  WITH CHECK ADD  CONSTRAINT [FK_Orders_Cashier] FOREIGN KEY([CashierID])
REFERENCES [dbo].[Employees] ([EmployeeID])
GO
ALTER TABLE [dbo].[Orders] CHECK CONSTRAINT [FK_Orders_Cashier]
GO
ALTER TABLE [dbo].[PasswordResetTokens]  WITH CHECK ADD FOREIGN KEY([CustomerID])
REFERENCES [dbo].[Customers] ([CustomerID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Payments]  WITH CHECK ADD FOREIGN KEY([CustomerID])
REFERENCES [dbo].[Customers] ([CustomerID])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Payments]  WITH CHECK ADD FOREIGN KEY([MethodID])
REFERENCES [dbo].[PaymentMethod] ([MethodID])
GO
ALTER TABLE [dbo].[Payments]  WITH CHECK ADD FOREIGN KEY([OrderID])
REFERENCES [dbo].[Orders] ([OrderID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Payments]  WITH CHECK ADD FOREIGN KEY([StatusID])
REFERENCES [dbo].[PaymentStatus] ([StatusID])
GO
USE [master]
GO
ALTER DATABASE [RESTAURANT] SET  READ_WRITE 
GO


	