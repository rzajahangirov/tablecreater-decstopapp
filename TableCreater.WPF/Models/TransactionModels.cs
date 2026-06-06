using System.ComponentModel.DataAnnotations;
using TableCreater.WPF.Enums;

namespace TableCreater.WPF.Models;

// ============================================================================
// TRANSACTION DTOs / Records
// Mapped from Java TransactionCreateDto, TransactionUpdateDto, TransactionReadDto,
// TransactionUpdateReadDto, and TranslationExpenseDto.
// ============================================================================

/// <summary>
/// Request model for creating a new transaction.
/// Mapped from Java TransactionCreateDto with Bean Validation annotations.
/// </summary>
public record TransactionCreateRequest
{
    [Required(ErrorMessage = "Tarix qeyd edilməlidir")]
    public DateOnly TransactionDate { get; init; }

    [Required(ErrorMessage = "Məhsul adı boş ola bilməz")]
    public string ProductName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Qəbul edən firma qeyd edilməlidir")]
    public string ReceivingCompany { get; init; } = string.Empty;

    [Required, Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Çəki mütləq 0-dan böyük olmalıdır")]
    public decimal WeightTon { get; init; }

    [Required, Range(0, (double)decimal.MaxValue, ErrorMessage = "Qiymət mənfi ola bilməz")]
    public decimal PricePerTonRub { get; init; }

    [Required(ErrorMessage = "Nəqliyyat növü seçilməlidir")]
    public TransportType TransportType { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Maşın/Gəmi sayı ən az 1 olmalıdır")]
    public int? VehicleCount { get; init; }

    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Nəqliyyat qiyməti mənfi ola bilməz")]
    public decimal? PricePerVehicle { get; init; }

    [Required(ErrorMessage = "Ödəniş valyutası mütləqdir")]
    public PaymentCurrency PaidCurrency { get; init; }

    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Ödənilən məbləğ mənfi ola bilməz")]
    public decimal? PaidAmount { get; init; }

    [Required, Range(0.0001, (double)decimal.MaxValue, ErrorMessage = "Məzənnə 0-dan böyük olmalıdır")]
    public decimal HistoricalExchangeRate { get; init; }

    /// <summary>
    /// Local file path selected via OpenFileDialog (replaces MultipartFile from Java).
    /// </summary>
    public string? DocumentFilePath { get; init; }
}

/// <summary>
/// Request model for updating an existing transaction.
/// Identical to TransactionCreateRequest with an additional IsCompleted flag.
/// Mapped from Java TransactionUpdateDto.
/// </summary>
public record TransactionUpdateRequest : TransactionCreateRequest
{
    public bool? IsCompleted { get; init; }
}

/// <summary>
/// Response model returned when reading a transaction.
/// Includes calculated historical financial data.
/// Mapped from Java TransactionReadDto.
/// </summary>
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
    public decimal PaidInUsd { get; init; }
    public bool IsCompleted { get; init; }
}

/// <summary>
/// Response model for populating the transaction edit form.
/// Mapped from Java TransactionUpdateReadDto.
/// </summary>
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

/// <summary>
/// Financial summary report for a date range or customer.
/// Mapped from Java TranslationExpenseDto.
/// </summary>
public record ExpenseIncomeReport
{
    public decimal TotalExpenseUsd { get; init; }
    public decimal TotalPaidUsd { get; init; }
    public decimal TotalBenefitUsd { get; init; }
    public long TransactionCount { get; init; }
}
