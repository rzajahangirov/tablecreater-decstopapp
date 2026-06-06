# THE DEVELOPER'S BIBLE — Master Specification
## TableCreater Accounting System: Java Spring Boot → C# .NET 9 WPF Migration
### Document Version: 1.0 | Generated: 2026-05-11

---

## TABLE OF CONTENTS — PART 1
1. Executive Summary & Architecture Overview
2. API/Endpoint Directory (Complete)
3. Full Data Contract Analysis (DTOs, Enums, Payloads)
4. Java DTO → C# Record/POCO Mapping

---

## 1. EXECUTIVE SUMMARY

### 1.1 Source System Profile
| Property | Value |
|---|---|
| **Project Name** | TableCreater (RCompany Management System) |
| **Java Version** | 17 |
| **Spring Boot** | 3.2.3 |
| **Database** | H2 (file-based) at `${user.home}/.tablecreater/data/tablecreater` |
| **ORM** | Spring Data JPA / Hibernate |
| **Frontend** | Vite/React SPA bundled into `static/assets/`, served by Spring Boot |
| **Desktop Shell** | JavaFX WebView loading `http://localhost:8080` |
| **Auth** | JWT (HMAC-SHA256, 8-hour access token, 10-min refresh token) |
| **File Upload** | MultipartFile → `uploads/` directory, max 10MB |
| **Excel Export** | Apache POI (XSSF) |
| **Object Mapping** | ModelMapper 3.2.0 |
| **Password Hashing** | BCrypt |
| **Main Class** | `TableCreaterApplication` → launches `FxLauncher` (JavaFX `Application`) |

### 1.2 Current Architecture Pattern
```
┌─────────────────────────────────────────────┐
│  JavaFX WebView (Desktop Shell)             │
│  ├── Loads http://localhost:8080             │
│  └── React SPA (Vite-bundled)               │
├─────────────────────────────────────────────┤
│  Spring Boot Embedded Server (port 8080)    │
│  ├── REST Controllers (JSON API)            │
│  ├── Service Layer (Business Logic)         │
│  ├── Repository Layer (Spring Data JPA)     │
│  └── H2 File Database                       │
└─────────────────────────────────────────────┘
```

### 1.3 Target Architecture
```
┌─────────────────────────────────────────────┐
│  WPF Application (.NET 9)                   │
│  ├── XAML Views (Native UI)                 │
│  ├── ViewModels (MVVM with CommunityToolkit)│
│  ├── Services (Business Logic, DI)          │
│  ├── EF Core DbContext                      │
│  └── SQLite File Database                   │
└─────────────────────────────────────────────┘
```

### 1.4 Key Migration Decisions
| Java Concept | C# Equivalent |
|---|---|
| Spring Boot Embedded Server | **ELIMINATED** — direct DB access |
| REST Controllers | **ELIMINATED** — ViewModels call Services directly |
| Spring Data JPA | EF Core 9 with SQLite provider |
| H2 Database | SQLite (`tablecreater.db`) |
| ModelMapper | Manual mapping or AutoMapper |
| Lombok | C# Records / auto-properties |
| JavaFX WebView | Native WPF XAML |
| Apache POI | ClosedXML or EPPlus |
| BCrypt (Spring Security) | `BCrypt.Net-Next` NuGet |
| JWT Auth | Local app — simplified to PIN/password at startup |
| MultipartFile | `Microsoft.Win32.OpenFileDialog` + `File.Copy` |
| `BigDecimal` | `decimal` (C# native 128-bit) |
| `LocalDate` | `DateOnly` (.NET 6+) |
| `LocalDateTime` | `DateTime` |
| `Instant` | `DateTimeOffset` |

---

## 2. API/ENDPOINT DIRECTORY (COMPLETE)

> **Migration Note:** In the WPF app, each endpoint becomes a **Service method** called directly by a ViewModel. There is no HTTP layer.

### 2.1 AuthController — `api/auth`

| # | HTTP | Path | Service Method | Business Purpose | WPF Equivalent |
|---|---|---|---|---|---|
| A1 | `GET` | `/api/auth/me` | `UserService.getLoggedUserInfo(email)` | Returns the currently authenticated user's profile (id, email, firstName, lastName, roleName, isActive). Requires valid JWT. | `IAuthService.GetCurrentUser()` — reads from in-memory session state |
| A2 | `POST` | `/api/auth/register` | `UserService.register(RegisterDto)` | Registers a new user. Hashes password with BCrypt. Maps DTO→Entity via ModelMapper. Saves to `users` table. | `IAuthService.Register(RegisterRequest)` |
| A3 | `POST` | `/api/auth/login` | `UserService.getLoggedUserInfo(email)` + `AuthenticationManager.authenticate()` + `RefreshTokenService.createRefreshToken()` + `JwtService.GenerateToken()` | Authenticates user with email/password. Checks `isActive` flag. Creates refresh token (UUID, 10-min expiry). Returns JWT access token (8-hour expiry) + refresh token. | `IAuthService.Login(string email, string password)` — verifies BCrypt hash, sets session |
| A4 | `POST` | `/api/auth/refreshToken` | `RefreshTokenService.findByToken()` → `verifyExpiration()` → `JwtService.GenerateToken()` | Validates a refresh token, checks expiry, generates new access token. Deletes expired tokens. | **ELIMINATED** — no JWT needed in desktop app |

**Auth Business Logic Details:**
- Password hashing: `BCryptPasswordEncoder.encode(password)`
- JWT secret: `357638792F423F4428472B4B6250655368566D597133743677397A2443264629` (Base64-decoded HMAC key)
- Access token TTL: 8 hours (`8 * 60 * 60 * 1000` ms)
- Refresh token TTL: 10 minutes (`600000` ms)
- Login flow: checks `isActive` → authenticates → creates refresh token → generates JWT
- UserDetails: loaded by email, maps roles to `SimpleGrantedAuthority`

### 2.2 CustomerController — `/api/customers`

| # | HTTP | Path | Service Method | Business Purpose | WPF Equivalent |
|---|---|---|---|---|---|
| C1 | `POST` | `/api/customers` | `CustomerService.createCustomer(CustomerCreateDto)` | Creates a new customer with name and phone. Default type = `ACTIVE`. | `ICustomerService.CreateCustomer(CustomerCreateRequest)` |
| C2 | `GET` | `/api/customers` | `CustomerService.getAllCustomers()` | Returns all customers as `List<CustomerReadDto>`. Returns empty list if none. | `ICustomerService.GetAllCustomers()` |
| C3 | `GET` | `/api/customers/{id}` | `CustomerService.getCustomerBydId(Long id)` | Returns a single customer by ID. Throws `RuntimeException("Customer not found")` if missing. | `ICustomerService.GetCustomerById(long id)` |
| C4 | `PATCH` | `/api/customers/{id}/status` | `CustomerService.changeStatus(Long id, CustomerType type)` | Changes a customer's status between `ACTIVE` and `INACTIVE`. Query param `type`. | `ICustomerService.ChangeStatus(long id, CustomerType type)` |
| C5 | `GET` | `/api/customers/search?keyword=` | `CustomerService.searchCustomers(String keyword)` | Searches customers by name OR phone (case-insensitive contains). Uses `findByNameContainingIgnoreCaseOrPhoneContainingIgnoreCase`. | `ICustomerService.SearchCustomers(string keyword)` |
| C6 | `PUT` | `/api/customers/update/{id}` | `CustomerService.updateCustomer(Long id, CustomerUpdateDto)` | Updates customer name and/or phone. Only updates non-null fields (partial update). | `ICustomerService.UpdateCustomer(long id, CustomerUpdateRequest)` |
| C7 | `DELETE` | `/api/customers/{id}` | `CustomerService.deleteCustomer(Long id)` | Deletes a customer by ID. Throws if not found. **CASCADE**: deletes all associated transactions. | `ICustomerService.DeleteCustomer(long id)` |

### 2.3 TransactionController — `/api/transaction`

| # | HTTP | Path | Service Method | Business Purpose | WPF Equivalent |
|---|---|---|---|---|---|
| T1 | `POST` | `/api/transaction/{customerId}` | `TransactionService.createTransaction(TransactionCreateDto, Long customerId)` | Creates a new transaction for a customer. Accepts `multipart/form-data` (includes optional file upload). Saves file to `uploads/` with UUID filename. Triggers `@PrePersist` calculation of historical financial data. | `ITransactionService.CreateTransaction(TransactionCreateRequest, long customerId)` |
| T2 | `GET` | `/api/transaction/{customerId}` | `TransactionService.getAllTranslationsByCustomer(Long customerId)` | Returns all transactions for a specific customer as `List<TransactionReadDto>`. | `ITransactionService.GetTransactionsByCustomer(long customerId)` |
| T3 | `GET` | `/api/transaction/expense-income?from=&to=` | `TransactionService.calculateExpenseAndIncome(LocalDate from, LocalDate to)` | **FINANCIAL REPORT**: Calculates total expense (USD), total paid (USD), net benefit, and transaction count for a date range. Validates `to >= from`. | `ITransactionService.CalculateExpenseAndIncome(DateOnly from, DateOnly to)` |
| T4 | `GET` | `/api/transaction/expense-income/{customerId}` | `TransactionService.calculateCustomerExpenseAndIncome(Long customerId)` | **CUSTOMER FINANCIAL SUMMARY**: Same calculation as T3 but for all transactions of a single customer (no date filter). | `ITransactionService.CalculateCustomerExpenseAndIncome(long customerId)` |
| T5 | `PUT` | `/api/transaction/{id}` | `TransactionService.updateTransaction(Long id, TransactionUpdateDto)` | Updates an existing transaction. Accepts `multipart/form-data`. Replaces all fields. Triggers `@PreUpdate` recalculation. Includes `isCompleted` flag. | `ITransactionService.UpdateTransaction(long id, TransactionUpdateRequest)` |
| T6 | `GET` | `/api/transaction/{id}/for-update` | `TransactionService.getTransactionForUpdate(Long id)` | Returns transaction data in edit-friendly format (`TransactionUpdateReadDto`). Used to populate the edit form. | `ITransactionService.GetTransactionForUpdate(long id)` |
| T7 | `DELETE` | `/api/transaction/{id}` | `TransactionService.deleteTransaction(Long id)` | Deletes a transaction by ID. Throws if not found. | `ITransactionService.DeleteTransaction(long id)` |
| T8 | `GET` | `/api/transaction/export/{customerId}` | `TransactionService.exportCustomerTransactions(Long customerId)` | **EXCEL EXPORT**: Generates `.xlsx` file with customer info header + all transactions. Returns as download attachment. | `ITransactionService.ExportCustomerTransactions(long customerId, string filePath)` |

### 2.4 HomeController — `/api/v1`

| # | HTTP | Path | Service Method | Business Purpose | WPF Equivalent |
|---|---|---|---|---|---|
| H1 | `GET` | `/api/v1/` | (inline) | Returns "Welcome to TableCreate API" string. Health check. | **ELIMINATED** — no HTTP server |

---

## 3. FULL DATA CONTRACT ANALYSIS

### 3.1 Enumerations

#### `TransportType`
```java
public enum TransportType {
    TRUCK,  // Tır — pricing in USD
    SHIP    // Gəmi — pricing in RUB
}
```
**Business Rule**: `TRUCK` transport costs are already in USD. `SHIP` transport costs are in RUB and must be converted to USD using `historicalExchangeRate`.

#### `PaymentCurrency`
```java
public enum PaymentCurrency {
    USD,
    RUB
}
```
**Business Rule**: When `paidCurrency == RUB`, the paid amount is converted to USD via `paidAmount * historicalExchangeRate`.

#### `CustomerType`
```java
public enum CustomerType {
    ACTIVE,
    INACTIVE
}
```

#### `ColumnInputType`
```java
public enum ColumnInputType {
    MANUAL,      // User enters value manually
    CALCULATED   // System computes via formula
}
```

#### `DataType`
```java
public enum DataType {
    TEXT, NUMBER, MONEY, DATE, IMAGE
}
```

### 3.2 Transaction DTOs

#### `TransactionCreateDto` (Request — multipart/form-data)
| Field | Java Type | Validation | Business Meaning |
|---|---|---|---|
| `transactionDate` | `LocalDate` | `@NotNull("Tarix qeyd edilməlidir")` | User-selected transaction date |
| `productName` | `String` | `@NotBlank("Məhsul adı boş ola bilməz")` | Product/goods name |
| `receivingCompany` | `String` | `@NotBlank("Qəbul edən firma qeyd edilməlidir")` | Company receiving goods |
| `weightTon` | `BigDecimal` | `@NotNull` + `@Positive("Çəki mütləq 0-dan böyük olmalıdır")` | Weight in tons |
| `pricePerTonRub` | `BigDecimal` | `@NotNull` + `@PositiveOrZero("Qiymət mənfi ola bilməz")` | Price per ton in Russian Rubles |
| `transportType` | `TransportType` | `@NotNull("Nəqliyyat növü seçilməlidir")` | TRUCK or SHIP |
| `vehicleCount` | `Integer` | `@Min(1, "Maşın/Gəmi sayı ən az 1 olmalıdır")` | Number of vehicles/ships |
| `pricePerVehicle` | `BigDecimal` | `@PositiveOrZero("Nəqliyyat qiyməti mənfi ola bilməz")` | Cost per vehicle (USD for TRUCK, RUB for SHIP) |
| `paidCurrency` | `PaymentCurrency` | `@NotNull("Ödəniş valyutası mütləqdir")` | Payment currency (USD/RUB) |
| `paidAmount` | `BigDecimal` | `@PositiveOrZero("Ödənilən məbləğ mənfi ola bilməz")` | Amount paid |
| `historicalExchangeRate` | `BigDecimal` | `@NotNull` + `@Positive("Məzənnə 0-dan böyük olmalıdır")` | RUB-to-USD exchange rate on transaction date |
| `document` | `MultipartFile` | (optional) | Attached document (PDF/JPG) |

#### `TransactionUpdateDto` (Request — multipart/form-data)
Identical to `TransactionCreateDto` with one additional field:
| Field | Java Type | Validation | Business Meaning |
|---|---|---|---|
| `isCompleted` | `Boolean` | (optional) | Marks transaction as completed |

#### `TransactionReadDto` (Response)
| Field | Java Type | Source | Business Meaning |
|---|---|---|---|
| `id` | `Long` | Entity PK | Transaction ID |
| `customerId` | `Long` | `transaction.getCustomer().getId()` | Parent customer ID |
| `customerName` | `String` | `transaction.getCustomer().getName()` | Parent customer name |
| `transactionDate` | `LocalDate` | Entity field | User-selected date |
| `createdAt` | `LocalDate` | Entity field (auto-set) | System creation date |
| `productName` | `String` | Entity field | Product name |
| `receivingCompany` | `String` | Entity field | Receiving company |
| `weightTon` | `BigDecimal` | Entity field | Weight in tons |
| `pricePerTonRub` | `BigDecimal` | Entity field | Price per ton (RUB) |
| `transportType` | `TransportType` | Entity field | TRUCK/SHIP |
| `vehicleCount` | `Integer` | Entity field | Vehicle count |
| `pricePerVehicle` | `BigDecimal` | Entity field | Price per vehicle |
| `paidAmount` | `BigDecimal` | Entity field | Amount paid |
| `paidCurrency` | `PaymentCurrency` | Entity field | Payment currency |
| `documentPath` | `String` | `transaction.getDocument()` | File path (e.g., `/uploads/uuid.pdf`) |
| `historicalExchangeRate` | `BigDecimal` | Entity field | Exchange rate at creation |
| `historicalTotalExpenseUsd` | `BigDecimal` | **CALCULATED** | Total cost in USD |
| `historicalRemainingDebtUsd` | `BigDecimal` | **CALCULATED** | Remaining debt in USD |

#### `TransactionUpdateReadDto` (Response — for edit form population)
| Field | Java Type |
|---|---|
| `transactionDate` | `LocalDate` |
| `productName` | `String` |
| `receivingCompany` | `String` |
| `weightTon` | `BigDecimal` |
| `pricePerTonRub` | `BigDecimal` |
| `transportType` | `TransportType` |
| `vehicleCount` | `Integer` |
| `pricePerVehicle` | `BigDecimal` |
| `paidCurrency` | `PaymentCurrency` |
| `paidAmount` | `BigDecimal` |
| `historicalExchangeRate` | `BigDecimal` |
| `documentImageUrl` | `String` |
| `isCompleted` | `Boolean` |

#### `TranslationExpenseDto` (Response — financial summary)
| Field | Java Type | Business Meaning |
|---|---|---|
| `totalExpenseUsd` | `BigDecimal` | Sum of all `historicalTotalExpenseUsd` |
| `totalPaidUsd` | `BigDecimal` | Sum of all payments converted to USD |
| `totalBenefitUsd` | `BigDecimal` | `totalPaidUsd - totalExpenseUsd` (profit/loss) |
| `transactionCount` | `long` | Number of transactions in range |

### 3.3 Customer DTOs

#### `CustomerCreateDto` (Request)
| Field | Java Type | Validation |
|---|---|---|
| `name` | `String` | (none) |
| `phone` | `String` | (none) |

#### `CustomerReadDto` (Response)
| Field | Java Type |
|---|---|
| `id` | `Long` |
| `name` | `String` |
| `phone` | `String` |

#### `CustomerUpdateDto` (Request)
| Field | Java Type | Update Logic |
|---|---|---|
| `name` | `String` | Only updates if non-null |
| `phone` | `String` | Only updates if non-null |

### 3.4 Auth DTOs

#### `LoginDto` (Request)
| Field | Java Type |
|---|---|
| `email` | `String` |
| `password` | `String` |

#### `RegisterDto` (Request)
| Field | Java Type |
|---|---|
| `name` | `String` |
| `surname` | `String` |
| `email` | `String` |
| `password` | `String` |

#### `JwtResponseDto` (Response)
| Field | Java Type | Business Meaning |
|---|---|---|
| `accessToken` | `String` | JWT access token |
| `token` | `String` | Refresh token (UUID) |

#### `UserLoggedDto` (Response)
| Field | Java Type |
|---|---|
| `id` | `Long` |
| `email` | `String` |
| `firstName` | `String` |
| `lastName` | `String` |
| `roleName` | `String` |
| `isActive` | `boolean` |

#### `RefreshTokenRequestDto` (Request)
| Field | Java Type |
|---|---|
| `token` | `String` |

#### `UserReadDto` (Response)
| Field | Java Type |
|---|---|
| `id` | `Long` |
| `username` | `String` |
| `email` | `String` |
| `active` | `boolean` |

### 3.5 Generic Payloads

#### `ResponseDto<T>` (Wrapper)
| Field | Java Type |
|---|---|
| `data` | `T` |
| `message` | `String` |

#### `ApiResponse` (Error/Success)
| Field | Java Type |
|---|---|
| `message` | `String` |
| `success` | `boolean` |

#### `AuthError` (Auth failure)
| Field | Java Type |
|---|---|
| `status` | `int` |
| `message` | `String` |

#### `PaginationPayload<T>` (Unused but defined)
| Field | Java Type |
|---|---|
| `success` | `boolean` |
| `pageNumber` | `int` |
| `pageSize` | `int` |
| `totalElements` | `long` |
| `totalPages` | `int` |
| `lastPage` | `boolean` |
| `data` | `List<T>` |

---

## 4. JAVA DTO → C# MAPPING

### 4.1 Enums (C#)
```csharp
public enum TransportType { Truck, Ship }
public enum PaymentCurrency { Usd, Rub }
public enum CustomerType { Active, Inactive }
public enum ColumnInputType { Manual, Calculated }
public enum DataType { Text, Number, Money, Date, Image }
```

### 4.2 Transaction C# Records

```csharp
// === CREATE REQUEST ===
public record TransactionCreateRequest
{
    [Required(ErrorMessage = "Tarix qeyd edilməlidir")]
    public DateOnly TransactionDate { get; init; }

    [Required(ErrorMessage = "Məhsul adı boş ola bilməz")]
    public string ProductName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Qəbul edən firma qeyd edilməlidir")]
    public string ReceivingCompany { get; init; } = string.Empty;

    [Required, Range(0.01, double.MaxValue, ErrorMessage = "Çəki mütləq 0-dan böyük olmalıdır")]
    public decimal WeightTon { get; init; }

    [Required, Range(0, double.MaxValue, ErrorMessage = "Qiymət mənfi ola bilməz")]
    public decimal PricePerTonRub { get; init; }

    [Required(ErrorMessage = "Nəqliyyat növü seçilməlidir")]
    public TransportType TransportType { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Maşın/Gəmi sayı ən az 1 olmalıdır")]
    public int? VehicleCount { get; init; }

    [Range(0, double.MaxValue, ErrorMessage = "Nəqliyyat qiyməti mənfi ola bilməz")]
    public decimal? PricePerVehicle { get; init; }

    [Required(ErrorMessage = "Ödəniş valyutası mütləqdir")]
    public PaymentCurrency PaidCurrency { get; init; }

    [Range(0, double.MaxValue, ErrorMessage = "Ödənilən məbləğ mənfi ola bilməz")]
    public decimal? PaidAmount { get; init; }

    [Required, Range(0.0001, double.MaxValue, ErrorMessage = "Məzənnə 0-dan böyük olmalıdır")]
    public decimal HistoricalExchangeRate { get; init; }

    public string? DocumentFilePath { get; init; }  // Local file path from OpenFileDialog
}

// === UPDATE REQUEST ===
public record TransactionUpdateRequest : TransactionCreateRequest
{
    public bool? IsCompleted { get; init; }
}

// === READ RESPONSE ===
public record TransactionReadResponse
{
    public long Id { get; init; }
    public long CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public DateOnly TransactionDate { get; init; }
    public DateOnly CreatedAt { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string ReceivingCompany { get; init; } = string.Empty;
    public decimal WeightTon { get; init; }
    public decimal PricePerTonRub { get; init; }
    public TransportType TransportType { get; init; }
    public int? VehicleCount { get; init; }
    public decimal? PricePerVehicle { get; init; }
    public decimal? PaidAmount { get; init; }
    public PaymentCurrency PaidCurrency { get; init; }
    public string? DocumentPath { get; init; }
    public decimal HistoricalExchangeRate { get; init; }
    public decimal HistoricalTotalExpenseUsd { get; init; }
    public decimal HistoricalRemainingDebtUsd { get; init; }
}

// === UPDATE READ (for edit form) ===
public record TransactionEditFormData
{
    public DateOnly TransactionDate { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string ReceivingCompany { get; init; } = string.Empty;
    public decimal WeightTon { get; init; }
    public decimal PricePerTonRub { get; init; }
    public TransportType TransportType { get; init; }
    public int? VehicleCount { get; init; }
    public decimal? PricePerVehicle { get; init; }
    public PaymentCurrency PaidCurrency { get; init; }
    public decimal? PaidAmount { get; init; }
    public decimal HistoricalExchangeRate { get; init; }
    public string? DocumentImageUrl { get; init; }
    public bool IsCompleted { get; init; }
}

// === FINANCIAL SUMMARY ===
public record ExpenseIncomeReport
{
    public decimal TotalExpenseUsd { get; init; }
    public decimal TotalPaidUsd { get; init; }
    public decimal TotalBenefitUsd { get; init; }
    public long TransactionCount { get; init; }
}
```

### 4.3 Customer C# Records
```csharp
public record CustomerCreateRequest
{
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
}

public record CustomerUpdateRequest
{
    public string? Name { get; init; }
    public string? Phone { get; init; }
}

public record CustomerReadResponse
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
}
```

### 4.4 Auth C# Records
```csharp
public record LoginRequest(string Email, string Password);

public record RegisterRequest(string Name, string Surname, string Email, string Password);

public record UserSessionInfo
{
    public long Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
```
