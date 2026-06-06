# THE DEVELOPER'S BIBLE — Master Specification
## TableCreater Accounting System: Java Spring Boot → C# .NET 9 WPF Migration
### Document Version: 1.0 | Generated: 2026-05-11

---

## TABLE OF CONTENTS — PART 2
5. Core Business Logic & Algorithms
6. Database & Persistence (EF Core & SQLite)
7. WPF UI & MVVM Architecture
8. Architecture Roadmap & Implementation Steps

---

## 5. CORE BUSINESS LOGIC & ALGORITHMS

The system's integrity relies on the "Historical Data" pattern. When a transaction is created, the values are locked in using the exchange rate of that specific day.

### 5.1 The Transaction Calculation Engine
In the C# migration, this logic must reside in the `TransactionService` or within the `Transaction` entity (using a domain-driven approach).

**Inputs:**
- `WeightTon` (decimal)
- `PricePerTonRub` (decimal)
- `TransportType` (Enum: Truck/Ship)
- `VehicleCount` (int)
- `PricePerVehicle` (decimal)
- `PaidAmount` (decimal)
- `PaidCurrency` (Enum: Usd/Rub)
- `HistoricalExchangeRate` (decimal)

**Algorithm:**
1. **Calculate Goods Cost (USD):**
   - `GoodsCostRub = WeightTon * PricePerTonRub`
   - `GoodsCostUsd = GoodsCostRub * HistoricalExchangeRate`
   
2. **Calculate Transport Cost (USD):**
   - `TotalTransportRaw = PricePerVehicle * VehicleCount`
   - IF `TransportType == Ship`:
     - `TransportCostUsd = TotalTransportRaw * HistoricalExchangeRate`
   - ELSE (`Truck`):
     - `TransportCostUsd = TotalTransportRaw` (Trucks are billed in USD)

3. **Calculate Total Expense (USD):**
   - `HistoricalTotalExpenseUsd = GoodsCostUsd + TransportCostUsd`

4. **Calculate Paid Amount in USD:**
   - IF `PaidCurrency == Rub`:
     - `PaidInUsd = PaidAmount * HistoricalExchangeRate`
   - ELSE (`Usd`):
     - `PaidInUsd = PaidAmount`

5. **Calculate Remaining Balance (USD):**
   - `HistoricalRemainingDebtUsd = PaidInUsd - HistoricalTotalExpenseUsd`
   - *Note: A negative value indicates debt to the supplier, positive indicates overpayment.*

### 5.2 Aggregate Financial Reporting
When calculating "Expense/Income" reports for a date range or customer:
- **Total Expense:** Sum of `HistoricalTotalExpenseUsd` for all selected transactions.
- **Total Paid:** Sum of `PaidInUsd` for all selected transactions.
- **Total Benefit:** `TotalPaid - TotalExpense`.

---

## 6. DATABASE & PERSISTENCE (EF CORE & SQLITE)

### 6.1 Entity Mapping (Java JPA → C# EF Core)

#### `User` & `Role`
- **User Table:** `Id` (PK), `Name`, `Surname`, `Email` (Unique), `Password` (Hashed).
- **Role Table:** `Id` (PK), `Name`.
- **Relationship:** Many-to-Many via `UserRoles` join table.

#### `Customer`
- **Properties:** `Id` (PK), `Name`, `Phone`, `Type` (Enum string).
- **Relationship:** One-to-Many with `Transactions`.

#### `Transaction`
- **Properties:** All DTO fields + `Id` (PK), `CustomerId` (FK), `IsCompleted` (bool), `CreatedAt` (DateTime).
- **Navigation:** `public Customer Customer { get; set; }`

#### `CustomColumn` & `CustomFieldValue` (Dynamic Schema)
- **CustomColumn:** `Id`, `Name`, `InputType` (Manual/Calc), `Formula` (string), `DataType`, `SortOrder`.
- **CustomFieldValue:** `Id`, `TransactionId` (FK), `CustomColumnId` (FK), `Value` (string).

### 6.2 SQLite Schema Definition
```sql
CREATE TABLE Customers (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Phone TEXT,
    Type TEXT DEFAULT 'ACTIVE'
);

CREATE TABLE Transactions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CustomerId INTEGER NOT NULL,
    TransactionDate TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    ProductName TEXT,
    ReceivingCompany TEXT,
    WeightTon REAL,
    PricePerTonRub REAL,
    TransportType TEXT,
    VehicleCount INTEGER,
    PricePerVehicle REAL,
    PaidAmount REAL,
    PaidCurrency TEXT,
    DocumentPath TEXT,
    HistoricalExchangeRate REAL NOT NULL,
    HistoricalTotalExpenseUsd REAL,
    HistoricalRemainingDebtUsd REAL,
    IsCompleted INTEGER DEFAULT 0,
    FOREIGN KEY (CustomerId) REFERENCES Customers(Id) ON DELETE CASCADE
);
```

---

## 7. WPF UI & MVVM ARCHITECTURE

### 7.1 Recommended Frameworks
- **UI Framework:** [ModernWpf](https://github.com/Kinnara/ModernWpf) (Best for Windows 11 aesthetics) or [MaterialDesignInXaml](http://materialdesigninxaml.net/).
- **MVVM Toolkit:** `CommunityToolkit.Mvvm` (Source generators for `ObservableProperty` and `RelayCommand`).
- **Dependency Injection:** `Microsoft.Extensions.DependencyInjection`.

### 7.2 Main ViewModels & Properties

#### `CustomerListViewModel`
- `ObservableCollection<CustomerReadResponse> Customers`
- `string SearchKeyword`
- `IAsyncRelayCommand SearchCommand`
- `IRelayCommand AddCustomerCommand`

#### `TransactionEntryViewModel`
- `TransactionCreateRequest Draft`
- `decimal LiveTotalExpenseUsd` (Calculated on-the-fly via algorithm in 5.1 as user types)
- `IAsyncRelayCommand SaveCommand`
- `IAsyncRelayCommand UploadFileCommand` (Uses `OpenFileDialog`)

#### `FinancialDashboardViewModel`
- `DateTime FromDate`, `DateTime ToDate`
- `ExpenseIncomeReport Summary`
- `IAsyncRelayCommand RefreshCommand`

### 7.3 Navigation Flow
1. **LoginView:** Validates user → Sets `CurrentUserInfo` in DI container → Navigates to `MainView`.
2. **MainView:** Side navigation menu (Customers, Reports, Settings).
3. **CustomerDetailView:** Lists transactions for a specific customer → Button to "Add Transaction" opens `TransactionEntryDialog`.

---

## 8. ARCHITECTURE ROADMAP & IMPLEMENTATION

### 8.1 Folder Structure (.NET Solution)
```text
TableCreater.WPF/
├── Data/
│   ├── AppDbContext.cs        (EF Core Context)
│   └── Entities/              (C# Entity Models)
├── Services/
│   ├── IAuthService.cs        (BCrypt, Session mgmt)
│   ├── ICustomerService.cs    (CRUD logic)
│   ├── ITransactionService.cs (Calc logic, CRUD)
│   └── IExcelService.cs       (ClosedXML export)
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── Customers/
│   └── Transactions/
├── Views/
│   ├── MainWindow.xaml
│   ├── Pages/
│   └── Dialogs/
├── Enums/
├── Converters/                (XAML Value Converters)
└── App.xaml.cs                (DI Container Setup)
```

### 8.2 Dependency Injection Setup (App.xaml.cs)
```csharp
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; }

    public App()
    {
        Services = ConfigureServices();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite("Data Source=tablecreater.db"));

        // Services
        services.AddSingleton<IAuthService, AuthService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ITransactionService, TransactionService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<CustomerListViewModel>();

        return services.BuildServiceProvider();
    }
}
```

### 8.3 Implementation Priority
1. **Step 1:** Define Entities and `AppDbContext`. Run first migration to create `tablecreater.db`.
2. **Step 2:** Port the `TransactionService` logic (the "Calculation Engine").
3. **Step 3:** Implement `AuthService` with BCrypt hashing for startup login.
4. **Step 4:** Build the "Customer Management" UI.
5. **Step 5:** Build the "Transaction Entry" form with live calculation previews.
6. **Step 6:** Implement Excel Export using `ClosedXML`.

---
**END OF MASTER SPECIFICATION**
