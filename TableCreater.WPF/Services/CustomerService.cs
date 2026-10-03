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
        // Convert initial balance to USD if provided
        decimal initialBalanceUsd = 0m;
        if (request.InitialBalance.HasValue && request.InitialBalance.Value != 0)
        {
            initialBalanceUsd = request.InitialBalanceCurrency == PaymentCurrency.Rub
                ? request.InitialBalance.Value * request.InitialExchangeRate
                : request.InitialBalance.Value;
        }

        var entity = new Customer
        {
            Name = request.Name,
            Phone = request.Phone,
            Type = CustomerType.Active,
            BalanceUsd = initialBalanceUsd
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
                AmountUsd = initialBalanceUsd,
                BalanceAfterUsd = initialBalanceUsd,
                Description = $"İlkin balans təyinatı: {request.InitialBalance.GetValueOrDefault()} {request.InitialBalanceCurrency}"
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
            .OrderBy(c => c.Name)
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
            .OrderBy(c => c.Name)
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
            BalanceUsd = entity.BalanceUsd
        };
    }

    // =========================================================================
    // C8 — BALANCE ADJUSTMENT (Manual Deposit / Withdrawal / Direct Set)
    // =========================================================================

    /// <inheritdoc />
    public async Task<CustomerReadResponse> AdjustBalance(long id, CustomerBalanceAdjustmentRequest request)
    {
        var entity = await _db.Customers.FindAsync(id)
            ?? throw new InvalidOperationException("Customer not found");

        // Convert amount to USD
        decimal amountUsd = request.Currency == PaymentCurrency.Rub
            ? request.Amount * request.ExchangeRate
            : request.Amount;

        decimal delta;
        switch (request.Type)
        {
            case BalanceTransactionType.ManualDeposit:
                delta = amountUsd;
                break;
            case BalanceTransactionType.ManualWithdrawal:
                delta = -amountUsd;
                break;
            case BalanceTransactionType.Adjustment:
                // Direct set: delta is the difference between new and old balance
                delta = amountUsd - entity.BalanceUsd;
                break;
            default:
                throw new ArgumentException($"Unsupported balance operation type: {request.Type}");
        }

        entity.BalanceUsd += delta;

        _db.Set<CustomerBalanceHistory>().Add(new CustomerBalanceHistory
        {
            CustomerId = entity.Id,
            CreatedAt = DateTime.UtcNow,
            Type = request.Type,
            AmountUsd = delta,
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
                else
                {
                    decimal rawPaid = h.Transaction.PaidAmount ?? 0m;
                    paidUsd = h.Transaction.PaidCurrency == PaymentCurrency.Rub
                        ? rawPaid * h.Transaction.HistoricalExchangeRate
                        : rawPaid;
                }
            }

            return new CustomerBalanceHistoryResponse
            {
                Id = h.Id,
                CustomerId = h.CustomerId,
                TransactionId = h.TransactionId,
                CreatedAt = h.CreatedAt,
                Type = h.Type,
                AmountUsd = h.AmountUsd,
                BalanceAfterUsd = h.BalanceAfterUsd,
                Description = h.Description,
                TransactionPaidAmountUsd = paidUsd
            };
        }).ToList();
    }
}
