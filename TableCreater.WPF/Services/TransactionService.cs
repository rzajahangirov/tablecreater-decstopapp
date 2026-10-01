using System.IO;
using Microsoft.EntityFrameworkCore;
using TableCreater.WPF.Data;
using TableCreater.WPF.Data.Entities;
using TableCreater.WPF.Enums;
using TableCreater.WPF.Models;

namespace TableCreater.WPF.Services;

/// <summary>
/// Full implementation of the Transaction service.
/// Ports the Java TransactionService including the core Calculation Engine (Section 5.1)
/// and Aggregate Financial Reporting (Section 5.2).
///
/// All methods are async to keep the WPF UI responsive.
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Directory where document attachments are stored.
    /// Replaces Java's "uploads/" directory with a local app-data folder.
    /// </summary>
    private static readonly string UploadsDirectory =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TableCreater", "uploads");

    public TransactionService(AppDbContext db)
    {
        _db = db;

        // Ensure the uploads directory exists on service construction
        if (!Directory.Exists(UploadsDirectory))
            Directory.CreateDirectory(UploadsDirectory);
    }

    // =========================================================================
    // T1 — CREATE TRANSACTION
    // Mapped from: TransactionService.createTransaction(TransactionCreateDto, Long)
    // Replaces Java @PrePersist with explicit calculation before SaveChanges.
    // =========================================================================

    /// <inheritdoc />
    public async Task<TransactionReadResponse> CreateTransaction(
        TransactionCreateRequest request, long customerId)
    {
        // Validate customer exists
        var customer = await _db.Customers.FindAsync(customerId)
            ?? throw new InvalidOperationException($"Customer not found (id={customerId})");

        // Handle document attachment (replaces MultipartFile → uploads/ logic)
        string? savedDocumentPath = CopyDocumentToUploads(request.DocumentFilePath);

        // Build entity from request
        var entity = new Transaction
        {
            CustomerId = customerId,
            TransactionDate = request.TransactionDate,
            CreatedAt = DateTime.UtcNow,
            ProductName = request.ProductName,
            ReceivingCompany = request.ReceivingCompany,
            SendingCompany = request.SendingCompany,
            WeightTon = request.WeightTon,
            PricePerTonRub = request.PricePerTonRub,
            TransportType = request.TransportType,
            TransportCurrency = request.TransportCurrency,
            VehicleCount = request.VehicleCount,
            PricePerVehicle = request.PricePerVehicle,
            PaidAmount = request.PaidAmount,
            PaidCurrency = request.PaidCurrency,
            HistoricalExchangeRate = request.HistoricalExchangeRate,
            DocumentPath = savedDocumentPath,
            AdditionalExpenseAmount = request.AdditionalExpenseAmount,
            AdditionalExpenseCurrency = request.AdditionalExpenseCurrency,
            AdditionalExpenseDescription = request.AdditionalExpenseDescription
        };

        // Sync and validate shipment status dates
        SyncShipmentStatusAndDates(
            entity,
            request.ShipmentStatus,
            request.LoadedDate,
            request.InTransitStartDate,
            request.DeliveredDate);

        // === CALCULATION ENGINE (replaces Java @PrePersist) ===
        CalculateHistoricalFields(entity);

        _db.Transactions.Add(entity);
        await _db.SaveChangesAsync();

        return MapToReadResponse(entity, customer.Name);
    }

    // =========================================================================
    // T2 — GET ALL TRANSACTIONS BY CUSTOMER
    // Mapped from: TransactionService.getAllTranslationsByCustomer(Long)
    // =========================================================================

    /// <inheritdoc />
    public async Task<List<TransactionReadResponse>> GetTransactionsByCustomer(long customerId)
    {
        var transactions = await _db.Transactions
            .Include(t => t.Customer)
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        return transactions.Select(t => MapToReadResponse(t, t.Customer.Name)).ToList();
    }

    // =========================================================================
    // T3 — CALCULATE EXPENSE AND INCOME (DATE RANGE)
    // Mapped from: TransactionService.calculateExpenseAndIncome(LocalDate, LocalDate)
    // Implements Section 5.2 — Aggregate Financial Reporting.
    // =========================================================================

    /// <inheritdoc />
    public async Task<ExpenseIncomeReport> CalculateExpenseAndIncome(DateOnly from, DateOnly to)
    {
        if (to < from)
            throw new ArgumentException("'to' date must be >= 'from' date.");

        var transactions = await _db.Transactions
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to)
            .ToListAsync();

        return BuildExpenseIncomeReport(transactions);
    }

    // =========================================================================
    // T4 — CALCULATE CUSTOMER EXPENSE AND INCOME
    // Mapped from: TransactionService.calculateCustomerExpenseAndIncome(Long)
    // Same aggregate logic as T3 but filtered by customer (no date filter).
    // =========================================================================

    /// <inheritdoc />
    public async Task<ExpenseIncomeReport> CalculateCustomerExpenseAndIncome(long customerId)
    {
        var transactions = await _db.Transactions
            .Where(t => t.CustomerId == customerId)
            .ToListAsync();

        return BuildExpenseIncomeReport(transactions);
    }

    // =========================================================================
    // T5 — UPDATE TRANSACTION
    // Mapped from: TransactionService.updateTransaction(Long, TransactionUpdateDto)
    // Replaces Java @PreUpdate with explicit recalculation before SaveChanges.
    // =========================================================================

    /// <inheritdoc />
    public async Task<TransactionReadResponse> UpdateTransaction(
        long id, TransactionUpdateRequest request)
    {
        var entity = await _db.Transactions
            .Include(t => t.Customer)
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new InvalidOperationException($"Transaction not found (id={id})");

        // Handle document: if a new file path is provided, copy it; otherwise keep existing
        string? savedDocumentPath = request.DocumentFilePath != null
            ? CopyDocumentToUploads(request.DocumentFilePath)
            : entity.DocumentPath;

        // Update all fields from request (full replace, not partial)
        entity.TransactionDate = request.TransactionDate;
        entity.ProductName = request.ProductName;
        entity.ReceivingCompany = request.ReceivingCompany;
        entity.SendingCompany = request.SendingCompany;
        entity.WeightTon = request.WeightTon;
        entity.PricePerTonRub = request.PricePerTonRub;
        entity.TransportType = request.TransportType;
        entity.TransportCurrency = request.TransportCurrency;
        entity.VehicleCount = request.VehicleCount;
        entity.PricePerVehicle = request.PricePerVehicle;
        entity.PaidAmount = request.PaidAmount;
        entity.PaidCurrency = request.PaidCurrency;
        entity.HistoricalExchangeRate = request.HistoricalExchangeRate;
        entity.DocumentPath = savedDocumentPath;
        entity.AdditionalExpenseAmount = request.AdditionalExpenseAmount;
        entity.AdditionalExpenseCurrency = request.AdditionalExpenseCurrency;
        entity.AdditionalExpenseDescription = request.AdditionalExpenseDescription;

        // Sync shipment tracking status and dates
        SyncShipmentStatusAndDates(
            entity,
            request.ShipmentStatus,
            request.LoadedDate,
            request.InTransitStartDate,
            request.DeliveredDate);

        if (request.IsCompleted.HasValue)
        {
            entity.IsCompleted = request.IsCompleted.Value;
        }

        // === RECALCULATION ENGINE (replaces Java @PreUpdate) ===
        CalculateHistoricalFields(entity);

        await _db.SaveChangesAsync();

        return MapToReadResponse(entity, entity.Customer.Name);
    }

    // =========================================================================
    // UPDATE SHIPMENT STATUS (Dedicated Endpoint / Action)
    // =========================================================================

    /// <inheritdoc />
    public async Task<TransactionReadResponse> UpdateShipmentStatus(
        long id, ShipmentStatusUpdateRequest request)
    {
        var entity = await _db.Transactions
            .Include(t => t.Customer)
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new InvalidOperationException($"Transaction not found (id={id})");

        SyncShipmentStatusAndDates(
            entity,
            request.Status,
            request.LoadedDate,
            request.InTransitStartDate,
            request.DeliveredDate);

        await _db.SaveChangesAsync();

        return MapToReadResponse(entity, entity.Customer.Name);
    }

    // =========================================================================
    // T6 — GET TRANSACTION FOR UPDATE (EDIT FORM)
    // Mapped from: TransactionService.getTransactionForUpdate(Long)
    // Returns data in the shape needed by the edit form.
    // =========================================================================

    /// <inheritdoc />
    public async Task<TransactionEditFormData> GetTransactionForUpdate(long id)
    {
        var entity = await _db.Transactions.FindAsync(id)
            ?? throw new InvalidOperationException($"Transaction not found (id={id})");

        return new TransactionEditFormData
        {
            TransactionDate = entity.TransactionDate,
            ProductName = entity.ProductName ?? string.Empty,
            ReceivingCompany = entity.ReceivingCompany ?? string.Empty,
            SendingCompany = entity.SendingCompany,
            WeightTon = entity.WeightTon,
            PricePerTonRub = entity.PricePerTonRub,
            TransportType = entity.TransportType,
            TransportCurrency = entity.TransportCurrency,
            VehicleCount = entity.VehicleCount,
            PricePerVehicle = entity.PricePerVehicle,
            PaidCurrency = entity.PaidCurrency,
            PaidAmount = entity.PaidAmount,
            HistoricalExchangeRate = entity.HistoricalExchangeRate,
            DocumentImageUrl = entity.DocumentPath,
            IsCompleted = entity.IsCompleted,
            ShipmentStatus = entity.ShipmentStatus,
            LoadedDate = entity.LoadedDate,
            InTransitStartDate = entity.InTransitStartDate,
            InTransitEndDate = entity.InTransitEndDate,
            DeliveredDate = entity.DeliveredDate,
            IsInTransitAutoDates = entity.IsInTransitAutoDates,
            AdditionalExpenseAmount = entity.AdditionalExpenseAmount,
            AdditionalExpenseCurrency = entity.AdditionalExpenseCurrency,
            AdditionalExpenseDescription = entity.AdditionalExpenseDescription
        };
    }

    // =========================================================================
    // T7 — DELETE TRANSACTION
    // Mapped from: TransactionService.deleteTransaction(Long)
    // =========================================================================

    /// <inheritdoc />
    public async Task DeleteTransaction(long id)
    {
        var entity = await _db.Transactions.FindAsync(id)
            ?? throw new InvalidOperationException($"Transaction not found (id={id})");

        _db.Transactions.Remove(entity);
        await _db.SaveChangesAsync();
    }

    // =========================================================================
    // T8 — EXPORT CUSTOMER TRANSACTIONS (Excel)
    // Mapped from: TransactionService.exportCustomerTransactions(Long)
    // Stub — delegates to IExcelService in a future step.
    // =========================================================================

    /// <inheritdoc />
    public async Task ExportCustomerTransactions(long customerId, string filePath)
    {
        // Full implementation will use ClosedXML in Step 6.
        // For now, validate the customer exists and that transactions are available.
        var customer = await _db.Customers.FindAsync(customerId)
            ?? throw new InvalidOperationException($"Customer not found (id={customerId})");

        var transactions = await _db.Transactions
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        if (transactions.Count == 0)
            throw new InvalidOperationException("No transactions to export.");

        // TODO: Step 6 — Implement ClosedXML export here.
        await Task.CompletedTask;
    }

    // =========================================================================
    //  CORE CALCULATION ENGINE — Section 5.1
    //  Replaces Java @PrePersist / @PreUpdate lifecycle callbacks.
    //
    //  This is the heart of the system's financial integrity.
    //  All values are "locked in" using the HistoricalExchangeRate at persist time.
    //
    //  UPDATED: Transport currency is now manual (TransportCurrency field).
    //           Additional expenses are included in total expense.
    // =========================================================================

    /// <summary>
    /// Calculates and sets the historical financial fields on a Transaction entity.
    /// Must be called before every save (create or update).
    ///
    /// Algorithm (updated):
    ///
    /// 1. GoodsCostRub  = WeightTon × PricePerTonRub
    /// 2. GoodsCostUsd  = GoodsCostRub × HistoricalExchangeRate
    /// 3. TransportRaw  = PricePerVehicle × VehicleCount
    /// 4. TransportUsd  = (TransportCurrency==RUB) ? TransportRaw × Rate : TransportRaw
    /// 5. AdditionalExpUsd = (AdditionalExpenseCurrency==RUB) ? Amount × Rate : Amount
    /// 6. TotalExpense   = GoodsCostUsd + TransportCostUsd + AdditionalExpenseUsd
    /// 7. PaidInUsd      = (PaidCurrency==RUB) ? PaidAmount × Rate : PaidAmount
    /// 8. RemainingDebt  = PaidInUsd − TotalExpense  [negative = debt, positive = overpayment]
    /// </summary>
    private static void CalculateHistoricalFields(Transaction entity)
    {
        decimal rate = entity.HistoricalExchangeRate;

        // ── Step 1 & 2: Goods Cost ──────────────────────────────────────────
        decimal goodsCostRub = entity.WeightTon * entity.PricePerTonRub;
        decimal goodsCostUsd = goodsCostRub * rate;

        // ── Step 3 & 4: Transport Cost ──────────────────────────────────────
        // Transport currency is now manually selected via TransportCurrency
        decimal pricePerVehicle = entity.PricePerVehicle ?? 0m;
        int vehicleCount = entity.VehicleCount ?? 0;
        decimal totalTransportRaw = pricePerVehicle * vehicleCount;

        decimal transportCostUsd = entity.TransportCurrency == PaymentCurrency.Rub
            ? totalTransportRaw * rate    // RUB → USD conversion
            : totalTransportRaw;          // Already in USD

        // ── Step 5: Additional Expense ──────────────────────────────────────
        decimal additionalExpenseUsd = 0m;
        if (entity.AdditionalExpenseAmount.HasValue && entity.AdditionalExpenseAmount.Value > 0)
        {
            decimal addAmount = entity.AdditionalExpenseAmount.Value;
            additionalExpenseUsd = (entity.AdditionalExpenseCurrency == PaymentCurrency.Rub)
                ? addAmount * rate    // RUB → USD conversion
                : addAmount;          // Already in USD (or null defaults to USD)
        }

        // ── Step 6: Total Expense (USD) ─────────────────────────────────────
        entity.HistoricalTotalExpenseUsd = goodsCostUsd + transportCostUsd + additionalExpenseUsd;

        // ── Step 7: Paid Amount in USD ──────────────────────────────────────
        decimal paidAmount = entity.PaidAmount ?? 0m;
        decimal paidInUsd = entity.PaidCurrency == PaymentCurrency.Rub
            ? paidAmount * rate           // RUB → USD conversion
            : paidAmount;                 // Already in USD

        // ── Step 8: Remaining Debt (USD) ────────────────────────────────────
        // Negative = debt to supplier, Positive = overpayment
        entity.HistoricalRemainingDebtUsd = paidInUsd - entity.HistoricalTotalExpenseUsd;
    }

    /// <summary>
    /// Computes the PaidInUsd for a single transaction from its raw fields.
    /// Used by aggregate reporting since PaidInUsd is not stored as a column.
    /// </summary>
    private static decimal ComputePaidInUsd(Transaction t)
    {
        decimal paidAmount = t.PaidAmount ?? 0m;
        return t.PaidCurrency == PaymentCurrency.Rub
            ? paidAmount * t.HistoricalExchangeRate
            : paidAmount;
    }

    /// <summary>
    /// Computes the AdditionalExpenseUsd for a single transaction from its raw fields.
    /// </summary>
    private static decimal ComputeAdditionalExpenseUsd(Transaction t)
    {
        if (!t.AdditionalExpenseAmount.HasValue || t.AdditionalExpenseAmount.Value <= 0)
            return 0m;

        return (t.AdditionalExpenseCurrency == PaymentCurrency.Rub)
            ? t.AdditionalExpenseAmount.Value * t.HistoricalExchangeRate
            : t.AdditionalExpenseAmount.Value;
    }

    // =========================================================================
    //  AGGREGATE FINANCIAL REPORTING — Section 5.2
    //  Builds an ExpenseIncomeReport from a set of transactions.
    // =========================================================================

    /// <summary>
    /// Builds the aggregate financial report from a list of transactions.
    ///
    /// - TotalExpenseUsd: Sum of HistoricalTotalExpenseUsd (pre-calculated on each entity)
    /// - TotalPaidUsd:    Sum of PaidInUsd (recomputed from raw PaidAmount + PaidCurrency + Rate)
    /// - TotalBenefitUsd: TotalPaidUsd − TotalExpenseUsd
    /// - TransactionCount: Number of transactions
    /// </summary>
    private static ExpenseIncomeReport BuildExpenseIncomeReport(List<Transaction> transactions)
    {
        decimal totalExpense = transactions.Sum(t => t.HistoricalTotalExpenseUsd);
        decimal totalPaid = transactions.Sum(ComputePaidInUsd);
        decimal totalBenefit = totalPaid - totalExpense;

        return new ExpenseIncomeReport
        {
            TotalExpenseUsd = totalExpense,
            TotalPaidUsd = totalPaid,
            TotalBenefitUsd = totalBenefit,
            TransactionCount = transactions.Count
        };
    }

    // =========================================================================
    //  MAPPING HELPERS
    // =========================================================================

    /// <summary>
    /// Maps a Transaction entity to a TransactionReadResponse DTO.
    /// Replaces Java's ModelMapper mapping.
    /// </summary>
    private static TransactionReadResponse MapToReadResponse(Transaction entity, string customerName)
    {
        return new TransactionReadResponse
        {
            Id = entity.Id,
            CustomerId = entity.CustomerId,
            CustomerName = customerName,
            TransactionDate = entity.TransactionDate,
            CreatedAt = DateOnly.FromDateTime(entity.CreatedAt),
            ProductName = entity.ProductName ?? string.Empty,
            ReceivingCompany = entity.ReceivingCompany ?? string.Empty,
            SendingCompany = entity.SendingCompany,
            WeightTon = entity.WeightTon,
            PricePerTonRub = entity.PricePerTonRub,
            TransportType = entity.TransportType,
            TransportCurrency = entity.TransportCurrency,
            VehicleCount = entity.VehicleCount,
            PricePerVehicle = entity.PricePerVehicle,
            PaidAmount = entity.PaidAmount,
            PaidCurrency = entity.PaidCurrency,
            DocumentPath = entity.DocumentPath,
            HistoricalExchangeRate = entity.HistoricalExchangeRate,
            HistoricalTotalExpenseUsd = entity.HistoricalTotalExpenseUsd,
            HistoricalRemainingDebtUsd = entity.HistoricalRemainingDebtUsd,
            PaidInUsd = ComputePaidInUsd(entity),
            IsCompleted = entity.IsCompleted,
            ShipmentStatus = entity.ShipmentStatus,
            LoadedDate = entity.LoadedDate,
            InTransitStartDate = entity.InTransitStartDate,
            InTransitEndDate = entity.InTransitEndDate,
            DeliveredDate = entity.DeliveredDate,
            IsInTransitAutoDates = entity.IsInTransitAutoDates,
            AdditionalExpenseAmount = entity.AdditionalExpenseAmount,
            AdditionalExpenseCurrency = entity.AdditionalExpenseCurrency,
            AdditionalExpenseDescription = entity.AdditionalExpenseDescription,
            AdditionalExpenseUsd = ComputeAdditionalExpenseUsd(entity)
        };
    }

    /// <summary>
    /// Synchronizes shipment status, transit date intervals, and completion flag.
    /// Stage 0 (Pending): All dates null.
    /// Stage 1 (Loaded): Stores LoadedDate.
    /// Stage 2 (InTransit): Stores InTransitStartDate (and optional LoadedDate). InTransitEndDate remains null.
    /// Stage 3 (Delivered): Stores DeliveredDate. InTransitEndDate is automatically set equal to DeliveredDate.
    /// </summary>
    private static void SyncShipmentStatusAndDates(
        Transaction entity,
        ShipmentStatus status,
        DateOnly? loadedDate,
        DateOnly? inTransitStartDate,
        DateOnly? deliveredDate)
    {
        entity.ShipmentStatus = status;

        switch (status)
        {
            case ShipmentStatus.Pending:
                entity.LoadedDate = null;
                entity.InTransitStartDate = null;
                entity.InTransitEndDate = null;
                entity.DeliveredDate = null;
                entity.IsCompleted = false;
                break;

            case ShipmentStatus.Loaded:
                if (!loadedDate.HasValue)
                    throw new InvalidOperationException("Yüklənmə tarixi mütləq qeyd edilməlidir.");
                entity.LoadedDate = loadedDate.Value;
                entity.InTransitStartDate = null;
                entity.InTransitEndDate = null;
                entity.DeliveredDate = null;
                entity.IsCompleted = false;
                break;

            case ShipmentStatus.InTransit:
                if (!inTransitStartDate.HasValue)
                    throw new InvalidOperationException("Yola çıxma tarixi mütləq qeyd edilməlidir.");
                entity.InTransitStartDate = inTransitStartDate.Value;
                entity.InTransitEndDate = null; // Yoldadır — hələ çatmayıb
                if (loadedDate.HasValue)
                {
                    entity.LoadedDate = loadedDate.Value;
                }
                entity.DeliveredDate = null;
                entity.IsCompleted = false;
                break;

            case ShipmentStatus.Delivered:
                if (!deliveredDate.HasValue)
                    throw new InvalidOperationException("Çatdırılma tarixi mütləq qeyd edilməlidir.");
                entity.DeliveredDate = deliveredDate.Value;
                // Stage 3 biznes qaydası: Yoldadır intervalının ikinci tarixi çatdı tarixi ilə eyni olacaq
                entity.InTransitEndDate = deliveredDate.Value;
                entity.InTransitStartDate = inTransitStartDate ?? entity.InTransitStartDate ?? entity.LoadedDate ?? entity.TransactionDate;
                if (loadedDate.HasValue)
                {
                    entity.LoadedDate = loadedDate.Value;
                }
                entity.IsCompleted = true;
                break;
        }
    }

    // =========================================================================
    //  FILE HANDLING
    //  Replaces Java MultipartFile → UUID-named file in uploads/ directory.
    // =========================================================================

    /// <summary>
    /// Copies a user-selected document file to the local uploads directory
    /// with a UUID-based filename to prevent collisions.
    /// Returns the saved path, or null if no file was provided.
    /// </summary>
    private static string? CopyDocumentToUploads(string? sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            return null;

        string extension = Path.GetExtension(sourceFilePath);
        string newFileName = $"{Guid.NewGuid()}{extension}";
        string destinationPath = Path.Combine(UploadsDirectory, newFileName);

        File.Copy(sourceFilePath, destinationPath, overwrite: true);

        return destinationPath;
    }
}
