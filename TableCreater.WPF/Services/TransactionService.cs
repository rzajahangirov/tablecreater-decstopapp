using System.IO;
using Microsoft.EntityFrameworkCore;
using TableCreater.WPF.Data;
using TableCreater.WPF.Data.Entities;
using TableCreater.WPF.Enums;
using TableCreater.WPF.Models;

namespace TableCreater.WPF.Services;

/// <summary>
/// Full implementation of the Transaction service.
/// Updated for dual-stream billing (RUB + USD independently) with
/// dynamic per-ton additional expenses.
/// All transactions automatically bill directly to the customer's balances.
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly AppDbContext _db;
    private readonly IExcelService? _excelService;

    /// <summary>
    /// Directory where document attachments are stored.
    /// Uses application base directory for portable isolation.
    /// </summary>
    private static readonly string UploadsDirectory =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uploads");

    public TransactionService(AppDbContext db, IExcelService? excelService = null)
    {
        _db = db;
        _excelService = excelService;

        if (!Directory.Exists(UploadsDirectory))
            Directory.CreateDirectory(UploadsDirectory);

        try
        {
            var oldUploads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TableCreater", "uploads");

            if (Directory.Exists(oldUploads))
            {
                foreach (var file in Directory.GetFiles(oldUploads))
                {
                    var dest = Path.Combine(UploadsDirectory, Path.GetFileName(file));
                    if (!File.Exists(dest))
                        File.Copy(file, dest, overwrite: true);
                }
            }
        }
        catch { }
    }

    // =========================================================================
    // T1 — CREATE TRANSACTION
    // =========================================================================

    /// <inheritdoc />
    public async Task<TransactionReadResponse> CreateTransaction(
        TransactionCreateRequest request, long customerId)
    {
        var customer = await _db.Customers.FindAsync(customerId)
            ?? throw new InvalidOperationException($"Customer not found (id={customerId})");

        string? savedDocumentPath = CopyDocumentToUploads(request.DocumentFilePath);

        decimal rate = request.HistoricalExchangeRate > 0 ? request.HistoricalExchangeRate : 1.0m;

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
            PaidAmount = null,
            PaidCurrency = request.PaidCurrency,
            HistoricalExchangeRate = rate,
            DocumentPath = savedDocumentPath,
            PaymentStatus = PaymentStatus.Paid,
            ProfitPerTon = request.ProfitPerTon,
            ProfitPerTonCurrency = request.ProfitPerTonCurrency
        };

        SyncShipmentStatusAndDates(
            entity,
            request.ShipmentStatus,
            request.LoadedDate,
            request.InTransitStartDate,
            request.DeliveredDate);

        // === CALCULATION ENGINE: Dual-Stream Billing ===
        decimal goodsCostRub = entity.WeightTon * entity.PricePerTonRub;
        decimal transportRaw = (entity.PricePerVehicle ?? 0m) * (entity.VehicleCount ?? 0);
        decimal transportCostRub = entity.TransportCurrency == PaymentCurrency.Rub ? transportRaw : 0m;
        decimal profitRub = entity.ProfitPerTonCurrency == PaymentCurrency.Rub ? (entity.WeightTon * entity.ProfitPerTon) : 0m;

        decimal transportCostUsd = entity.TransportCurrency == PaymentCurrency.Usd ? transportRaw : 0m;
        decimal profitUsd = entity.ProfitPerTonCurrency == PaymentCurrency.Usd ? (entity.WeightTon * entity.ProfitPerTon) : 0m;

        decimal addlExpRub = 0m;
        decimal addlExpUsd = 0m;
        if (request.AdditionalExpenses != null && request.AdditionalExpenses.Count > 0)
        {
            foreach (var item in request.AdditionalExpenses)
            {
                decimal totalItemAmount = item.Type == AdditionalExpenseType.PerTon 
                    ? (item.AmountPerTon * entity.WeightTon) 
                    : item.AmountPerTon;

                if (item.Currency == PaymentCurrency.Rub)
                    addlExpRub += totalItemAmount;
                else
                    addlExpUsd += totalItemAmount;

                entity.AdditionalExpenses.Add(new TransactionAdditionalExpense
                {
                    Title = string.IsNullOrWhiteSpace(item.Title) ? "Əlavə xərc" : item.Title,
                    ExpenseType = item.Type,
                    AmountPerTon = item.AmountPerTon,
                    Currency = item.Currency
                });
            }
        }

        decimal baseExpenseRub = goodsCostRub + transportCostRub + addlExpRub;
        decimal baseExpenseUsd = transportCostUsd + addlExpUsd;

        entity.TotalBilledRub = baseExpenseRub + profitRub;
        entity.TotalBilledUsd = baseExpenseUsd + profitUsd;

        decimal unifiedBaseCostUsd = baseExpenseUsd + (rate > 0 ? baseExpenseRub / rate : 0m);
        decimal unifiedProfitUsd = profitUsd + (rate > 0 ? profitRub / rate : 0m);
        decimal unifiedBilledUsd = entity.TotalBilledUsd + (rate > 0 ? entity.TotalBilledRub / rate : 0m);

        entity.HistoricalTotalExpenseUsd = unifiedBaseCostUsd;
        entity.HistoricalCustomerBilledUsd = unifiedBilledUsd;
        entity.HistoricalUserProfitUsd = unifiedProfitUsd;

        _db.Transactions.Add(entity);
        await _db.SaveChangesAsync();

        // === BALANCE DEDUCTION: Automatic direct charge to customer accounts ===
        if (entity.TotalBilledRub > 0)
        {
            customer.BalanceRub -= entity.TotalBilledRub;
            _db.Set<CustomerBalanceHistory>().Add(new CustomerBalanceHistory
            {
                CustomerId = customer.Id,
                TransactionId = entity.Id,
                CreatedAt = DateTime.UtcNow,
                Type = BalanceTransactionType.TransactionCharge,
                Currency = PaymentCurrency.Rub,
                Amount = -entity.TotalBilledRub,
                BalanceAfter = customer.BalanceRub,
                AmountUsd = 0m,
                BalanceAfterUsd = customer.BalanceUsd,
                Description = $"Tranzaksiya #{entity.Id} — {entity.ProductName} (RUB xərci: -{entity.TotalBilledRub:N2} ₽)"
            });
        }

        if (entity.TotalBilledUsd > 0)
        {
            customer.BalanceUsd -= entity.TotalBilledUsd;
            _db.Set<CustomerBalanceHistory>().Add(new CustomerBalanceHistory
            {
                CustomerId = customer.Id,
                TransactionId = entity.Id,
                CreatedAt = DateTime.UtcNow,
                Type = BalanceTransactionType.TransactionCharge,
                Currency = PaymentCurrency.Usd,
                Amount = -entity.TotalBilledUsd,
                BalanceAfter = customer.BalanceUsd,
                AmountUsd = -entity.TotalBilledUsd,
                BalanceAfterUsd = customer.BalanceUsd,
                Description = $"Tranzaksiya #{entity.Id} — {entity.ProductName} (USD xərci: -${entity.TotalBilledUsd:N2})"
            });
        }

        await _db.SaveChangesAsync();
        return MapToReadResponse(entity, customer.Name);
    }

    // =========================================================================
    // T2 — GET ALL TRANSACTIONS BY CUSTOMER
    // =========================================================================

    /// <inheritdoc />
    public async Task<List<TransactionReadResponse>> GetTransactionsByCustomer(long customerId)
    {
        var transactions = await _db.Transactions
            .Include(t => t.Customer)
            .Include(t => t.AdditionalExpenses)
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        return transactions.Select(t => MapToReadResponse(t, t.Customer?.Name ?? string.Empty)).ToList();
    }

    // =========================================================================
    // T3 — CALCULATE EXPENSE AND INCOME (DATE RANGE)
    // =========================================================================

    /// <inheritdoc />
    public async Task<ExpenseIncomeReport> CalculateExpenseAndIncome(DateOnly from, DateOnly to)
    {
        if (to < from)
            throw new ArgumentException("'to' date must be >= 'from' date.");

        var transactions = await _db.Transactions
            .Include(t => t.AdditionalExpenses)
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to)
            .ToListAsync();

        return BuildExpenseIncomeReport(transactions);
    }

    // =========================================================================
    // T4 — CALCULATE CUSTOMER EXPENSE AND INCOME
    // =========================================================================

    /// <inheritdoc />
    public async Task<ExpenseIncomeReport> CalculateCustomerExpenseAndIncome(long customerId)
    {
        var transactions = await _db.Transactions
            .Include(t => t.AdditionalExpenses)
            .Where(t => t.CustomerId == customerId)
            .ToListAsync();

        return BuildExpenseIncomeReport(transactions);
    }

    /// <inheritdoc />
    public async Task<List<TransactionReadResponse>> GetTransactionsByDateRange(DateOnly from, DateOnly to)
    {
        if (to < from)
            throw new ArgumentException("'to' date must be >= 'from' date.");

        var transactions = await _db.Transactions
            .Include(t => t.Customer)
            .Include(t => t.AdditionalExpenses)
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        return transactions.Select(t => MapToReadResponse(t, t.Customer?.Name ?? string.Empty)).ToList();
    }

    // =========================================================================
    // T5 — UPDATE TRANSACTION
    // =========================================================================

    /// <inheritdoc />
    public async Task<TransactionReadResponse> UpdateTransaction(
        long id, TransactionUpdateRequest request)
    {
        var entity = await _db.Transactions
            .Include(t => t.Customer)
            .Include(t => t.AdditionalExpenses)
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new InvalidOperationException($"Transaction not found (id={id})");

        var customer = entity.Customer;

        // Roll back old charges
        decimal oldBilledRub = entity.TotalBilledRub;
        decimal oldBilledUsd = entity.TotalBilledUsd;

        customer.BalanceRub += oldBilledRub;
        customer.BalanceUsd += oldBilledUsd;

        // Document handling
        string? savedDocumentPath = request.DocumentFilePath != null
            ? CopyDocumentToUploads(request.DocumentFilePath)
            : entity.DocumentPath;

        // Update fields
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
        decimal rate = request.HistoricalExchangeRate > 0 ? request.HistoricalExchangeRate : 1.0m;
        entity.HistoricalExchangeRate = rate;
        entity.DocumentPath = savedDocumentPath;
        entity.ProfitPerTon = request.ProfitPerTon;
        entity.ProfitPerTonCurrency = request.ProfitPerTonCurrency;

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

        // Remove old dynamic additional expenses
        _db.TransactionAdditionalExpenses.RemoveRange(entity.AdditionalExpenses);
        entity.AdditionalExpenses.Clear();

        // Calculate new dual stream
        decimal goodsCostRub = entity.WeightTon * entity.PricePerTonRub;
        decimal transportRaw = (entity.PricePerVehicle ?? 0m) * (entity.VehicleCount ?? 0);
        decimal transportCostRub = entity.TransportCurrency == PaymentCurrency.Rub ? transportRaw : 0m;
        decimal profitRub = entity.ProfitPerTonCurrency == PaymentCurrency.Rub ? (entity.WeightTon * entity.ProfitPerTon) : 0m;

        decimal transportCostUsd = entity.TransportCurrency == PaymentCurrency.Usd ? transportRaw : 0m;
        decimal profitUsd = entity.ProfitPerTonCurrency == PaymentCurrency.Usd ? (entity.WeightTon * entity.ProfitPerTon) : 0m;

        decimal addlExpRub = 0m;
        decimal addlExpUsd = 0m;
        if (request.AdditionalExpenses != null && request.AdditionalExpenses.Count > 0)
        {
            foreach (var item in request.AdditionalExpenses)
            {
                decimal totalItemAmount = item.Type == AdditionalExpenseType.PerTon 
                    ? (item.AmountPerTon * entity.WeightTon) 
                    : item.AmountPerTon;

                if (item.Currency == PaymentCurrency.Rub)
                    addlExpRub += totalItemAmount;
                else
                    addlExpUsd += totalItemAmount;

                entity.AdditionalExpenses.Add(new TransactionAdditionalExpense
                {
                    TransactionId = entity.Id,
                    Title = string.IsNullOrWhiteSpace(item.Title) ? "Əlavə xərc" : item.Title,
                    ExpenseType = item.Type,
                    AmountPerTon = item.AmountPerTon,
                    Currency = item.Currency
                });
            }
        }

        decimal baseExpenseRub = goodsCostRub + transportCostRub + addlExpRub;
        decimal baseExpenseUsd = transportCostUsd + addlExpUsd;

        entity.TotalBilledRub = baseExpenseRub + profitRub;
        entity.TotalBilledUsd = baseExpenseUsd + profitUsd;

        decimal unifiedBaseCostUsd = baseExpenseUsd + (rate > 0 ? baseExpenseRub / rate : 0m);
        decimal unifiedProfitUsd = profitUsd + (rate > 0 ? profitRub / rate : 0m);
        decimal unifiedBilledUsd = entity.TotalBilledUsd + (rate > 0 ? entity.TotalBilledRub / rate : 0m);

        entity.HistoricalTotalExpenseUsd = unifiedBaseCostUsd;
        entity.HistoricalCustomerBilledUsd = unifiedBilledUsd;
        entity.HistoricalUserProfitUsd = unifiedProfitUsd;

        // Apply new charges
        customer.BalanceRub -= entity.TotalBilledRub;
        customer.BalanceUsd -= entity.TotalBilledUsd;

        decimal netRubDiff = oldBilledRub - entity.TotalBilledRub;
        if (netRubDiff != 0)
        {
            _db.Set<CustomerBalanceHistory>().Add(new CustomerBalanceHistory
            {
                CustomerId = customer.Id,
                TransactionId = entity.Id,
                CreatedAt = DateTime.UtcNow,
                Type = BalanceTransactionType.TransactionUpdate,
                Currency = PaymentCurrency.Rub,
                Amount = netRubDiff,
                BalanceAfter = customer.BalanceRub,
                AmountUsd = 0m,
                BalanceAfterUsd = customer.BalanceUsd,
                Description = $"Tranzaksiya #{entity.Id} yeniləndi (RUB balansı dəyişimi: {netRubDiff:+#,##0.00;-#,##0.00;0.00} ₽)"
            });
        }

        decimal netUsdDiff = oldBilledUsd - entity.TotalBilledUsd;
        if (netUsdDiff != 0)
        {
            _db.Set<CustomerBalanceHistory>().Add(new CustomerBalanceHistory
            {
                CustomerId = customer.Id,
                TransactionId = entity.Id,
                CreatedAt = DateTime.UtcNow,
                Type = BalanceTransactionType.TransactionUpdate,
                Currency = PaymentCurrency.Usd,
                Amount = netUsdDiff,
                BalanceAfter = customer.BalanceUsd,
                AmountUsd = netUsdDiff,
                BalanceAfterUsd = customer.BalanceUsd,
                Description = $"Tranzaksiya #{entity.Id} yeniləndi (USD balansı dəyişimi: {netUsdDiff:+#,##0.00;-#,##0.00;0.00} $)"
            });
        }

        await _db.SaveChangesAsync();
        return MapToReadResponse(entity, customer.Name);
    }

    /// <inheritdoc />
    public async Task<TransactionReadResponse> UpdateShipmentStatus(
        long id, ShipmentStatusUpdateRequest request)
    {
        var entity = await _db.Transactions
            .Include(t => t.Customer)
            .Include(t => t.AdditionalExpenses)
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new InvalidOperationException($"Transaction not found (id={id})");

        SyncShipmentStatusAndDates(
            entity,
            request.Status,
            request.LoadedDate,
            request.InTransitStartDate,
            request.DeliveredDate);

        await _db.SaveChangesAsync();
        return MapToReadResponse(entity, entity.Customer?.Name ?? string.Empty);
    }

    /// <inheritdoc />
    public async Task<TransactionReadResponse> ToggleTransactionCompleted(long id)
    {
        var entity = await _db.Transactions
            .Include(t => t.Customer)
            .Include(t => t.AdditionalExpenses)
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new InvalidOperationException($"Transaction not found (id={id})");

        entity.IsCompleted = !entity.IsCompleted;
        await _db.SaveChangesAsync();

        return MapToReadResponse(entity, entity.Customer?.Name ?? string.Empty);
    }

    // =========================================================================
    // T6 — GET TRANSACTION FOR UPDATE
    // =========================================================================

    /// <inheritdoc />
    public async Task<TransactionEditFormData> GetTransactionForUpdate(long id)
    {
        var entity = await _db.Transactions
            .Include(t => t.AdditionalExpenses)
            .FirstOrDefaultAsync(t => t.Id == id)
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
            HistoricalExchangeRate = entity.HistoricalExchangeRate,
            DocumentImageUrl = entity.DocumentPath,
            IsCompleted = entity.IsCompleted,
            ProfitPerTon = entity.ProfitPerTon,
            ProfitPerTonCurrency = entity.ProfitPerTonCurrency,
            ShipmentStatus = entity.ShipmentStatus,
            LoadedDate = entity.LoadedDate,
            InTransitStartDate = entity.InTransitStartDate,
            InTransitEndDate = entity.InTransitEndDate,
            DeliveredDate = entity.DeliveredDate,
            IsInTransitAutoDates = entity.IsInTransitAutoDates,
            AdditionalExpenses = entity.AdditionalExpenses?.Select(e => new TransactionAdditionalExpenseDto
            {
                Title = e.Title,
                Type = e.ExpenseType,
                AmountPerTon = e.AmountPerTon,
                Currency = e.Currency,
                TotalAmount = e.ExpenseType == AdditionalExpenseType.Fixed 
                    ? e.AmountPerTon 
                    : (entity.WeightTon * e.AmountPerTon)
            }).ToList() ?? new List<TransactionAdditionalExpenseDto>()
        };
    }

    // =========================================================================
    // T7 — DELETE TRANSACTION
    // =========================================================================

    /// <inheritdoc />
    public async Task DeleteTransaction(long id)
    {
        var entity = await _db.Transactions
            .Include(t => t.Customer)
            .Include(t => t.AdditionalExpenses)
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new InvalidOperationException($"Transaction not found (id={id})");

        var customer = entity.Customer;

        if (entity.TotalBilledRub > 0)
        {
            customer.BalanceRub += entity.TotalBilledRub;
            _db.Set<CustomerBalanceHistory>().Add(new CustomerBalanceHistory
            {
                CustomerId = customer.Id,
                TransactionId = entity.Id,
                CreatedAt = DateTime.UtcNow,
                Type = BalanceTransactionType.TransactionRollback,
                Currency = PaymentCurrency.Rub,
                Amount = entity.TotalBilledRub,
                BalanceAfter = customer.BalanceRub,
                AmountUsd = 0m,
                BalanceAfterUsd = customer.BalanceUsd,
                Description = $"Tranzaksiya #{entity.Id} silindi — RUB bərpa edildi (+{entity.TotalBilledRub:N2} ₽)"
            });
        }

        if (entity.TotalBilledUsd > 0)
        {
            customer.BalanceUsd += entity.TotalBilledUsd;
            _db.Set<CustomerBalanceHistory>().Add(new CustomerBalanceHistory
            {
                CustomerId = customer.Id,
                TransactionId = entity.Id,
                CreatedAt = DateTime.UtcNow,
                Type = BalanceTransactionType.TransactionRollback,
                Currency = PaymentCurrency.Usd,
                Amount = entity.TotalBilledUsd,
                BalanceAfter = customer.BalanceUsd,
                AmountUsd = entity.TotalBilledUsd,
                BalanceAfterUsd = customer.BalanceUsd,
                Description = $"Tranzaksiya #{entity.Id} silindi — USD bərpa edildi (+${entity.TotalBilledUsd:N2})"
            });
        }

        _db.TransactionAdditionalExpenses.RemoveRange(entity.AdditionalExpenses);
        _db.Transactions.Remove(entity);
        await _db.SaveChangesAsync();
    }

    // =========================================================================
    // T8 — EXPORT CUSTOMER TRANSACTIONS (Excel)
    // =========================================================================

    /// <inheritdoc />
    public async Task ExportCustomerTransactions(long customerId, string filePath)
    {
        var customer = await _db.Customers.FindAsync(customerId)
            ?? throw new InvalidOperationException($"Customer not found (id={customerId})");

        var hasTransactions = await _db.Transactions.AnyAsync(t => t.CustomerId == customerId);
        if (!hasTransactions)
            throw new InvalidOperationException("İxrac ediləcək heç bir tranzaksiya yoxdur.");

        var excel = _excelService ?? new ExcelService(_db);
        await excel.ExportToExcel(customerId, filePath);
    }

    // =========================================================================
    //  AGGREGATE FINANCIAL REPORTING
    // =========================================================================

    private static ExpenseIncomeReport BuildExpenseIncomeReport(List<Transaction> transactions)
    {
        decimal totalBilledRub = transactions.Sum(t => t.TotalBilledRub);
        decimal totalBilledUsd = transactions.Sum(t => t.TotalBilledUsd);
        decimal totalWeight = transactions.Sum(t => t.WeightTon);
        int totalVehicles = transactions.Sum(t => t.VehicleCount ?? 0);

        decimal totalCustomerBilledUsd = transactions.Sum(t => 
            t.TotalBilledUsd + (t.HistoricalExchangeRate > 0 ? t.TotalBilledRub / t.HistoricalExchangeRate : 0m));

        decimal totalUserProfitUsd = transactions.Sum(t => t.HistoricalUserProfitUsd);

        decimal totalExpenseUsd = transactions.Sum(t => 
            Math.Max(0m, (t.TotalBilledUsd + (t.HistoricalExchangeRate > 0 ? t.TotalBilledRub / t.HistoricalExchangeRate : 0m)) - t.HistoricalUserProfitUsd));

        return new ExpenseIncomeReport
        {
            TotalExpenseUsd = totalExpenseUsd,
            TotalBilledUsd = totalCustomerBilledUsd,
            TotalUserProfitUsd = totalUserProfitUsd,
            TotalPaidUsd = totalCustomerBilledUsd,
            TotalBenefitUsd = totalUserProfitUsd,
            TotalCashFlowUsd = totalUserProfitUsd,
            TransactionCount = transactions.Count,
            PaidTransactionCount = transactions.Count,
            UnpaidTransactionCount = 0,
            TotalWeightTon = totalWeight,
            TotalVehicleCount = totalVehicles,
            TotalBilledRubSum = totalBilledRub,
            TotalBilledUsdSum = totalBilledUsd
        };
    }

    // =========================================================================
    //  MAPPING HELPERS
    // =========================================================================

    private static TransactionReadResponse MapToReadResponse(Transaction entity, string customerName)
    {
        var expDtos = entity.AdditionalExpenses?.Select(e => new TransactionAdditionalExpenseDto
        {
            Title = e.Title,
            Type = e.ExpenseType,
            AmountPerTon = e.AmountPerTon,
            Currency = e.Currency,
            TotalAmount = e.ExpenseType == AdditionalExpenseType.Fixed 
                ? e.AmountPerTon 
                : (entity.WeightTon * e.AmountPerTon)
        }).ToList() ?? new List<TransactionAdditionalExpenseDto>();

        decimal expRubTotal = expDtos.Where(e => e.Currency == PaymentCurrency.Rub).Sum(e => e.TotalAmount);
        decimal expUsdTotal = expDtos.Where(e => e.Currency == PaymentCurrency.Usd).Sum(e => e.TotalAmount);

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
            HistoricalRemainingDebtUsd = 0m,
            PaidInUsd = entity.TotalBilledUsd,
            IsCompleted = entity.IsCompleted,
            PaymentStatus = entity.PaymentStatus,
            PaidFromUsdAmount = entity.PaidFromUsdAmount,
            PaidFromRubAmount = entity.PaidFromRubAmount,
            ProfitPerTon = entity.ProfitPerTon,
            ProfitPerTonCurrency = entity.ProfitPerTonCurrency,
            HistoricalUserProfitUsd = entity.HistoricalUserProfitUsd,
            HistoricalCustomerBilledUsd = entity.HistoricalCustomerBilledUsd,
            HistoricalBalanceDeltaUsd = -entity.TotalBilledUsd,
            TotalBilledRub = entity.TotalBilledRub,
            TotalBilledUsd = entity.TotalBilledUsd,
            ShipmentStatus = entity.ShipmentStatus,
            LoadedDate = entity.LoadedDate,
            InTransitStartDate = entity.InTransitStartDate,
            InTransitEndDate = entity.InTransitEndDate,
            DeliveredDate = entity.DeliveredDate,
            IsInTransitAutoDates = entity.IsInTransitAutoDates,
            AdditionalExpenses = expDtos,
            AdditionalExpensesRubTotal = expRubTotal,
            AdditionalExpensesUsdTotal = expUsdTotal
        };
    }

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
                entity.LoadedDate = loadedDate;
                entity.InTransitStartDate = null;
                entity.InTransitEndDate = null;
                entity.DeliveredDate = null;
                entity.IsCompleted = false;
                break;

            case ShipmentStatus.InTransit:
                entity.InTransitStartDate = inTransitStartDate;
                entity.InTransitEndDate = null;
                if (loadedDate.HasValue)
                {
                    entity.LoadedDate = loadedDate.Value;
                }
                entity.DeliveredDate = null;
                entity.IsCompleted = false;
                break;

            case ShipmentStatus.Delivered:
                entity.DeliveredDate = deliveredDate;
                entity.InTransitEndDate = deliveredDate;
                if (inTransitStartDate.HasValue)
                {
                    entity.InTransitStartDate = inTransitStartDate.Value;
                }
                if (loadedDate.HasValue)
                {
                    entity.LoadedDate = loadedDate.Value;
                }
                entity.IsCompleted = true;
                break;
        }
    }

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
