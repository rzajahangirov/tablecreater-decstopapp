using Microsoft.EntityFrameworkCore;
using TableCreater.WPF.Data;
using TableCreater.WPF.Data.Entities;
using TableCreater.WPF.Enums;
using TableCreater.WPF.Models;

namespace TableCreater.WPF.Services;

/// <summary>
/// Customer service implementation — full CRUD + search + status toggle.
/// Ports Java CustomerService endpoints C1–C7 (Section 2.2).
/// All methods are async for UI responsiveness.
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly AppDbContext _db;

    public CustomerService(AppDbContext db)
    {
        _db = db;
    }

    // =========================================================================
    // C1 — CREATE CUSTOMER
    // Mapped from: CustomerService.createCustomer(CustomerCreateDto)
    // Default type = ACTIVE.
    // =========================================================================

    /// <inheritdoc />
    public async Task<CustomerReadResponse> CreateCustomer(CustomerCreateRequest request)
    {
        decimal initialBalanceUsd = 0m;
        decimal initialBalanceRub = 0m;

        if (request.InitialBalance.HasValue && request.InitialBalance.Value != 0)
        {
            if (request.InitialBalanceCurrency == PaymentCurrency.Rub)
                initialBalanceRub = request.InitialBalance.Value;
            else
                initialBalanceUsd = request.InitialBalance.Value;
        }

        var entity = new Customer
        {
            Name = request.Name,
            Phone = request.Phone,
            Type = CustomerType.Active,
            BalanceUsd = initialBalanceUsd,
            BalanceRub = initialBalanceRub
        };

        _db.Customers.Add(entity);
        await _db.SaveChangesAsync();

        // Record initial balance in ledger if non-zero
        if (initialBalanceUsd != 0)
        {
            _db.Set<CustomerBalanceHistory>().Add(new CustomerBalanceHistory
            {
                CustomerId = entity.Id,
                CreatedAt = DateTime.UtcNow,
                Type = BalanceTransactionType.Initial,
                Currency = PaymentCurrency.Usd,
                Amount = initialBalanceUsd,
                BalanceAfter = initialBalanceUsd,
                AmountUsd = initialBalanceUsd,
                BalanceAfterUsd = initialBalanceUsd,
                Description = $"İlkin balans təyinatı: ${initialBalanceUsd:N2}"
            });
            await _db.SaveChangesAsync();
        }
        else if (initialBalanceRub != 0)
        {
            _db.Set<CustomerBalanceHistory>().Add(new CustomerBalanceHistory
            {
                CustomerId = entity.Id,
                CreatedAt = DateTime.UtcNow,
                Type = BalanceTransactionType.Initial,
                Currency = PaymentCurrency.Rub,
                Amount = initialBalanceRub,
                BalanceAfter = initialBalanceRub,
                AmountUsd = 0m,
                BalanceAfterUsd = 0m,
                Description = $"İlkin balans təyinatı: {initialBalanceRub:N2} ₽"
            });
            await _db.SaveChangesAsync();
        }

        return MapToReadResponse(entity);
    }

    // =========================================================================
    // C2 — GET ALL CUSTOMERS
    // Mapped from: CustomerService.getAllCustomers()
    // Returns empty list if none exist.
    // =========================================================================

    /// <inheritdoc />
    public async Task<List<CustomerReadResponse>> GetAllCustomers()
    {
        var customers = await _db.Customers
            .OrderBy(c => c.Type == CustomerType.Active ? 0 : 1)
            .ThenBy(c => c.Name)
            .ToListAsync();

        return customers.Select(MapToReadResponse).ToList();
    }

    // =========================================================================
    // C3 — GET CUSTOMER BY ID
    // Mapped from: CustomerService.getCustomerBydId(Long id)
    // Throws if not found (matches Java RuntimeException behavior).
    // =========================================================================

    /// <inheritdoc />
    public async Task<CustomerReadResponse> GetCustomerById(long id)
    {
        var entity = await _db.Customers.FindAsync(id)
            ?? throw new InvalidOperationException("Customer not found");

        return MapToReadResponse(entity);
    }

    // =========================================================================
    // C4 — CHANGE STATUS
    // Mapped from: CustomerService.changeStatus(Long id, CustomerType type)
    // Toggles between ACTIVE and INACTIVE.
    // =========================================================================

    /// <inheritdoc />
    public async Task ChangeStatus(long id, CustomerType type)
    {
        var entity = await _db.Customers.FindAsync(id)
            ?? throw new InvalidOperationException("Customer not found");

        entity.Type = type;
        await _db.SaveChangesAsync();
    }

    // =========================================================================
    // C5 — SEARCH CUSTOMERS
    // Mapped from: CustomerService.searchCustomers(String keyword)
    // Case-insensitive search on Name OR Phone.
    // Matches Java: findByNameContainingIgnoreCaseOrPhoneContainingIgnoreCase
    // =========================================================================

    /// <inheritdoc />
    public async Task<List<CustomerReadResponse>> SearchCustomers(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return await GetAllCustomers();

        var lowerKeyword = keyword.ToLower();

        var results = await _db.Customers
            .Where(c => c.Name.ToLower().Contains(lowerKeyword)
                      || (c.Phone != null && c.Phone.ToLower().Contains(lowerKeyword)))
            .OrderBy(c => c.Type == CustomerType.Active ? 0 : 1)
            .ThenBy(c => c.Name)
            .ToListAsync();

        return results.Select(MapToReadResponse).ToList();
    }

    // =========================================================================
    // C6 — UPDATE CUSTOMER
    // Mapped from: CustomerService.updateCustomer(Long id, CustomerUpdateDto)
    // Partial update: only updates non-null fields.
    // =========================================================================

    /// <inheritdoc />
    public async Task<CustomerReadResponse> UpdateCustomer(long id, CustomerUpdateRequest request)
    {
        var entity = await _db.Customers.FindAsync(id)
            ?? throw new InvalidOperationException("Customer not found");

        // Partial update pattern — only overwrite if value is provided
        if (request.Name != null)
            entity.Name = request.Name;

        if (request.Phone != null)
            entity.Phone = request.Phone;

        if (request.Type.HasValue)
            entity.Type = request.Type.Value;

        await _db.SaveChangesAsync();

        return MapToReadResponse(entity);
    }

    // =========================================================================
    // C7 — DELETE CUSTOMER
    // Mapped from: CustomerService.deleteCustomer(Long id)
    // CASCADE DELETE: all associated transactions are deleted by EF Core
    // (configured in AppDbContext.OnModelCreating with DeleteBehavior.Cascade).
    // =========================================================================

    /// <inheritdoc />
    public async Task DeleteCustomer(long id)
    {
        var entity = await _db.Customers.FindAsync(id)
            ?? throw new InvalidOperationException("Customer not found");

        _db.Customers.Remove(entity);
        await _db.SaveChangesAsync();
    }

    // =========================================================================
    // MAPPING
    // =========================================================================

    private static CustomerReadResponse MapToReadResponse(Customer entity)
    {
        return new CustomerReadResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            Phone = entity.Phone ?? string.Empty,
            BalanceUsd = entity.BalanceUsd,
            BalanceRub = entity.BalanceRub,
            Type = entity.Type
        };
    }

    // =========================================================================
    // C8 — BALANCE ADJUSTMENT (Manual Deposit / Withdrawal / Direct Set / Transfer)
    // =========================================================================

    /// <inheritdoc />
    public async Task<CustomerReadResponse> AdjustBalance(long id, CustomerBalanceAdjustmentRequest request)
    {
        var entity = await _db.Customers.FindAsync(id)
            ?? throw new InvalidOperationException("Müştəri tapılmadı.");

        decimal amount = request.Amount;
        var currency = request.Currency;

        if (request.Type == BalanceTransactionType.Transfer)
        {
            // Valyutalar arası köçürmə (USD <-> RUB)
            decimal rate = request.ExchangeRate > 0 ? request.ExchangeRate : 1.0m;

            if (currency == PaymentCurrency.Usd)
            {
                // USD -> RUB
                decimal usdOut = amount;
                decimal rubIn = usdOut * rate;

                entity.BalanceUsd -= usdOut;
                entity.BalanceRub += rubIn;

                string desc = !string.IsNullOrWhiteSpace(request.Description)
                    ? request.Description
                    : $"Valyuta Köçürməsi: {usdOut:N2} USD → {rubIn:N2} RUB (Məzənnə: {rate:N2})";

                var histUsd = new CustomerBalanceHistory
                {
                    CustomerId = entity.Id,
                    CreatedAt = DateTime.UtcNow,
                    Type = BalanceTransactionType.Transfer,
                    Currency = PaymentCurrency.Usd,
                    Amount = -usdOut,
                    BalanceAfter = entity.BalanceUsd,
                    AmountUsd = -usdOut,
                    BalanceAfterUsd = entity.BalanceUsd,
                    Description = desc
                };

                var histRub = new CustomerBalanceHistory
                {
                    CustomerId = entity.Id,
                    CreatedAt = DateTime.UtcNow,
                    Type = BalanceTransactionType.Transfer,
                    Currency = PaymentCurrency.Rub,
                    Amount = rubIn,
                    BalanceAfter = entity.BalanceRub,
                    AmountUsd = 0m,
                    BalanceAfterUsd = entity.BalanceUsd,
                    Description = desc
                };

                _db.Set<CustomerBalanceHistory>().Add(histUsd);
                _db.Set<CustomerBalanceHistory>().Add(histRub);
                await _db.SaveChangesAsync();

                // Link both entries
                histUsd.RelatedHistoryId = histRub.Id;
                histRub.RelatedHistoryId = histUsd.Id;
                await _db.SaveChangesAsync();
            }
            else
            {
                // RUB -> USD
                decimal rubOut = amount;
                decimal usdIn = rate > 0 ? rubOut / rate : 0m;

                entity.BalanceRub -= rubOut;
                entity.BalanceUsd += usdIn;

                string desc = !string.IsNullOrWhiteSpace(request.Description)
                    ? request.Description
                    : $"Valyuta Köçürməsi: {rubOut:N2} RUB → {usdIn:N2} USD (Məzənnə: {rate:N2})";

                var histRub = new CustomerBalanceHistory
                {
                    CustomerId = entity.Id,
                    CreatedAt = DateTime.UtcNow,
                    Type = BalanceTransactionType.Transfer,
                    Currency = PaymentCurrency.Rub,
                    Amount = -rubOut,
                    BalanceAfter = entity.BalanceRub,
                    AmountUsd = 0m,
                    BalanceAfterUsd = entity.BalanceUsd,
                    Description = desc
                };

                var histUsd = new CustomerBalanceHistory
                {
                    CustomerId = entity.Id,
                    CreatedAt = DateTime.UtcNow,
                    Type = BalanceTransactionType.Transfer,
                    Currency = PaymentCurrency.Usd,
                    Amount = usdIn,
                    BalanceAfter = entity.BalanceUsd,
                    AmountUsd = usdIn,
                    BalanceAfterUsd = entity.BalanceUsd,
                    Description = desc
                };

                _db.Set<CustomerBalanceHistory>().Add(histRub);
                _db.Set<CustomerBalanceHistory>().Add(histUsd);
                await _db.SaveChangesAsync();

                // Link both entries
                histRub.RelatedHistoryId = histUsd.Id;
                histUsd.RelatedHistoryId = histRub.Id;
                await _db.SaveChangesAsync();
            }

            return MapToReadResponse(entity);
        }

        // Adi manual əməliyyat (Mədaxil, Məxaric, Düzəliş)
        decimal delta;
        decimal balanceAfter;

        if (currency == PaymentCurrency.Rub)
        {
            switch (request.Type)
            {
                case BalanceTransactionType.ManualDeposit:
                    delta = amount;
                    entity.BalanceRub += delta;
                    balanceAfter = entity.BalanceRub;
                    break;
                case BalanceTransactionType.ManualWithdrawal:
                    delta = -amount;
                    entity.BalanceRub += delta;
                    balanceAfter = entity.BalanceRub;
                    break;
                case BalanceTransactionType.Adjustment:
                    delta = amount - entity.BalanceRub;
                    entity.BalanceRub = amount;
                    balanceAfter = entity.BalanceRub;
                    break;
                default:
                    throw new ArgumentException($"Dəstəklənməyən balans əməliyyatı: {request.Type}");
            }
        }
        else
        {
            switch (request.Type)
            {
                case BalanceTransactionType.ManualDeposit:
                    delta = amount;
                    entity.BalanceUsd += delta;
                    balanceAfter = entity.BalanceUsd;
                    break;
                case BalanceTransactionType.ManualWithdrawal:
                    delta = -amount;
                    entity.BalanceUsd += delta;
                    balanceAfter = entity.BalanceUsd;
                    break;
                case BalanceTransactionType.Adjustment:
                    delta = amount - entity.BalanceUsd;
                    entity.BalanceUsd = amount;
                    balanceAfter = entity.BalanceUsd;
                    break;
                default:
                    throw new ArgumentException($"Dəstəklənməyən balans əməliyyatı: {request.Type}");
            }
        }

        _db.Set<CustomerBalanceHistory>().Add(new CustomerBalanceHistory
        {
            CustomerId = entity.Id,
            CreatedAt = DateTime.UtcNow,
            Type = request.Type,
            Currency = currency,
            Amount = delta,
            BalanceAfter = balanceAfter,
            AmountUsd = currency == PaymentCurrency.Usd ? delta : 0m,
            BalanceAfterUsd = entity.BalanceUsd,
            Description = request.Description ?? request.Type.ToString()
        });

        await _db.SaveChangesAsync();

        return MapToReadResponse(entity);
    }

    // =========================================================================
    // C9 — GET BALANCE HISTORY
    // =========================================================================

    /// <inheritdoc />
    public async Task<List<CustomerBalanceHistoryResponse>> GetBalanceHistory(long customerId)
    {
        var histories = await _db.Set<CustomerBalanceHistory>()
            .Include(h => h.Transaction)
            .Where(h => h.CustomerId == customerId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();

        return histories.Select(h =>
        {
            decimal? paidUsd = null;
            if (h.Transaction != null)
            {
                if (h.Transaction.PaymentStatus == PaymentStatus.Unpaid)
                {
                    paidUsd = 0m;
                }
                else if (h.Transaction.PaymentStatus == PaymentStatus.PaidFromBalance)
                {
                    decimal usdPart = h.Transaction.PaidFromUsdAmount ?? 0m;
                    decimal rubPart = h.Transaction.PaidFromRubAmount ?? 0m;
                    decimal rate = h.Transaction.HistoricalExchangeRate;
                    paidUsd = usdPart + (rate > 0 ? rubPart / rate : 0m);
                }
                else
                {
                    decimal rawPaid = h.Transaction.PaidAmount ?? 0m;
                    decimal rate = h.Transaction.HistoricalExchangeRate;
                    paidUsd = h.Transaction.PaidCurrency == PaymentCurrency.Rub
                        ? (rate > 0 ? rawPaid / rate : 0m)
                        : rawPaid;
                }
            }

            decimal effectiveAmount = h.Amount != 0 ? h.Amount : h.AmountUsd;
            decimal effectiveBalanceAfter = h.BalanceAfter != 0 ? h.BalanceAfter : h.BalanceAfterUsd;

            return new CustomerBalanceHistoryResponse
            {
                Id = h.Id,
                CustomerId = h.CustomerId,
                TransactionId = h.TransactionId,
                CreatedAt = h.CreatedAt,
                Type = h.Type,
                Currency = h.Currency,
                Amount = effectiveAmount,
                BalanceAfter = effectiveBalanceAfter,
                AmountUsd = h.AmountUsd,
                BalanceAfterUsd = h.BalanceAfterUsd,
                RelatedHistoryId = h.RelatedHistoryId,
                Description = h.Description,
                TransactionPaidAmountUsd = paidUsd
            };
        }).ToList();
    }

    // =========================================================================
    // C10 — DELETE BALANCE HISTORY (Revert manual deposit/withdrawal/adjustment)
    // =========================================================================

    /// <inheritdoc />
    public async Task<CustomerReadResponse> DeleteBalanceHistory(long historyId)
    {
        var history = await _db.Set<CustomerBalanceHistory>().FindAsync(historyId)
            ?? throw new InvalidOperationException("Balans əməliyyatı tapılmadı.");

        if (history.TransactionId.HasValue ||
            history.Type == BalanceTransactionType.TransactionCharge ||
            history.Type == BalanceTransactionType.TransactionUpdate ||
            history.Type == BalanceTransactionType.TransactionRollback)
        {
            throw new InvalidOperationException("Tranzaksiya ilə əlaqəli əməliyyatlar birbaşa buradan silinə bilməz. Tranzaksiyanın özünü redaktə edin və ya silin.");
        }

        var customer = await _db.Customers.FindAsync(history.CustomerId)
            ?? throw new InvalidOperationException("Müştəri tapılmadı.");

        // Əgər köçürmədirsə, hər iki qeydi geri qaytar və sil
        if (history.Type == BalanceTransactionType.Transfer && history.RelatedHistoryId.HasValue)
        {
            var related = await _db.Set<CustomerBalanceHistory>().FindAsync(history.RelatedHistoryId.Value);

            // Revert history
            RevertHistoryAmount(customer, history);

            if (related != null)
            {
                RevertHistoryAmount(customer, related);
                _db.Set<CustomerBalanceHistory>().Remove(related);
            }

            _db.Set<CustomerBalanceHistory>().Remove(history);
        }
        else
        {
            // Adi əməliyyat (Mədaxil, Məxaric, Düzəliş, İlkin balans)
            RevertHistoryAmount(customer, history);
            _db.Set<CustomerBalanceHistory>().Remove(history);
        }

        await _db.SaveChangesAsync();

        return MapToReadResponse(customer);
    }

    private static void RevertHistoryAmount(Customer customer, CustomerBalanceHistory h)
    {
        decimal amt = h.Amount != 0 ? h.Amount : h.AmountUsd;
        if (h.Currency == PaymentCurrency.Rub)
        {
            customer.BalanceRub -= amt;
        }
        else
        {
            customer.BalanceUsd -= amt;
        }
    }
}
