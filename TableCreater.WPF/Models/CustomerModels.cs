using TableCreater.WPF.Enums;

namespace TableCreater.WPF.Models;

// ============================================================================
// CUSTOMER DTOs / Records
// Mapped from Java CustomerCreateDto, CustomerReadDto, CustomerUpdateDto.
// ============================================================================

/// <summary>
/// Request model for creating a new customer.
/// Supports optional initial balance with currency selection.
/// </summary>
public record CustomerCreateRequest
{
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;

    // === İkili İlkin Balans (hər iki kassaya eyni anda depozit) ===
    public decimal InitialBalanceUsd { get; init; }
    public decimal InitialBalanceRub { get; init; }
}

/// <summary>
/// Request model for updating an existing customer.
/// Only non-null fields are applied (partial update pattern).
/// </summary>
public record CustomerUpdateRequest
{
    public string? Name { get; init; }
    public string? Phone { get; init; }
    public CustomerType? Type { get; init; }
}

/// <summary>
/// Response model returned when reading a customer.
/// </summary>
public record CustomerReadResponse
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public decimal BalanceUsd { get; init; }
    public decimal BalanceRub { get; init; }
    public CustomerType Type { get; init; } = CustomerType.Active;
}

/// <summary>
/// Request model for manual balance operations (Deposit, Withdrawal, Direct Adjustment, Transfer).
/// </summary>
public record CustomerBalanceAdjustmentRequest
{
    public BalanceTransactionType Type { get; init; } = BalanceTransactionType.ManualDeposit;
    public decimal Amount { get; init; }
    public PaymentCurrency Currency { get; init; } = PaymentCurrency.Usd;
    public PaymentCurrency TargetCurrency { get; init; } = PaymentCurrency.Rub;
    public decimal ExchangeRate { get; init; } = 1.0m;
    public string? Description { get; init; }
    /// <summary>
    /// Əməliyyat tarixi. Null olduqda cari vaxt istifadə olunur.
    /// </summary>
    public DateTime? OperationDate { get; init; }
}

/// <summary>
/// Balans tarixçəsində yalnız tarix və təsvirin redaktə edilməsi üçün sorğu modeli.
/// Maliyyə dəyərləri və qalıqlar toxunulmaz qalır.
/// </summary>
public record UpdateBalanceHistoryRequest
{
    public long HistoryId { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Response model for customer balance ledger history.
/// </summary>
public record CustomerBalanceHistoryResponse
{
    public long Id { get; init; }
    public long CustomerId { get; init; }
    public long? TransactionId { get; init; }
    public DateTime CreatedAt { get; init; }
    public BalanceTransactionType Type { get; init; }
    public PaymentCurrency Currency { get; init; } = PaymentCurrency.Usd;
    public decimal Amount { get; init; }
    public decimal BalanceAfter { get; init; }
    public decimal AmountUsd { get; init; }
    public decimal BalanceAfterUsd { get; init; }
    public long? RelatedHistoryId { get; init; }
    public string? Description { get; init; }
    public decimal? TransactionPaidAmountUsd { get; init; }

    /// <summary>
    /// Əməliyyat zamanı istifadə olunan məzənnə (məs. Valyuta köçürməsi zamanı USD/RUB məzənnəsi).
    /// </summary>
    public decimal? ExchangeRate { get; init; }

    /// <summary>
    /// Məzənnənin formatlanmış mətni (məsələn: "95.50 ₽" və ya "—").
    /// </summary>
    public string ExchangeRateFormatted => ExchangeRate.HasValue && ExchangeRate.Value > 0
        ? $"{ExchangeRate.Value:N2} ₽"
        : "—";

    /// <summary>
    /// Əməliyyatın manual olub-olmadığını və silinə biləcəyini göstərir.
    /// Tranzaksiyalara bağlı olmayan əməliyyatlar silinə bilər.
    /// </summary>
    public bool CanDelete => Type != BalanceTransactionType.TransactionCharge &&
                             Type != BalanceTransactionType.TransactionUpdate &&
                             Type != BalanceTransactionType.TransactionRollback &&
                             TransactionId == null;

    /// <summary>
    /// Əməliyyatın tarix və təsvirinin redaktə edilə biləcəyini göstərir.
    /// </summary>
    public bool CanEdit => true;

    public string KassaName => Currency == PaymentCurrency.Rub ? "RUB Kassası" : "USD Kassası";

    public string FormattedAmount
    {
        get
        {
            decimal amt = Amount != 0 ? Amount : AmountUsd;
            string sign = amt > 0 ? "+" : "";
            return Currency == PaymentCurrency.Rub
                ? $"{sign}{amt:N2} ₽"
                : $"{sign}${amt:N2}";
        }
    }

    public string FormattedBalanceAfter
    {
        get
        {
            decimal bal = BalanceAfter != 0 ? BalanceAfter : BalanceAfterUsd;
            return Currency == PaymentCurrency.Rub
                ? $"{bal:N2} ₽"
                : $"${bal:N2}";
        }
    }
}

