using System.ComponentModel.DataAnnotations;
using TableCreater.WPF.Enums;
using TableCreater.WPF.Data.Entities;

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

    /// <summary>
    /// Göndərən firma (sending company). Optional.
    /// </summary>
    public string? SendingCompany { get; init; }

    [Required, Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Çəki mütləq 0-dan böyük olmalıdır")]
    public decimal WeightTon { get; init; }

    [Required, Range(0, (double)decimal.MaxValue, ErrorMessage = "Qiymət mənfi ola bilməz")]
    public decimal PricePerTonRub { get; init; }

    [Required(ErrorMessage = "Nəqliyyat növü seçilməlidir")]
    public TransportType TransportType { get; init; }

    /// <summary>
    /// Nəqliyyat valyutası — manual seçilir, artıq avtomatik deyil.
    /// </summary>
    [Required(ErrorMessage = "Nəqliyyat valyutası seçilməlidir")]
    public PaymentCurrency TransportCurrency { get; init; } = PaymentCurrency.Usd;

    [Range(1, int.MaxValue, ErrorMessage = "Vasitə sayı ən az 1 olmalıdır")]
    public int? VehicleCount { get; init; }

    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Nəqliyyat qiyməti mənfi ola bilməz")]
    public decimal? PricePerVehicle { get; init; }

    [Required(ErrorMessage = "Ödəniş valyutası mütləqdir")]
    public PaymentCurrency PaidCurrency { get; init; }

    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Ödənilən məbləğ mənfi ola bilməz")]
    public decimal? PaidAmount { get; init; }

    [Required, Range(0.0001, (double)decimal.MaxValue, ErrorMessage = "Məzənnə 0-dan böyük olmalıdır")]
    public decimal HistoricalExchangeRate { get; init; }

    // === Əlavə Xərclər (Additional Expenses) ===

    /// <summary>
    /// Əlavə xərc məbləği. Nullable — mütləq deyil.
    /// </summary>
    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Əlavə xərc mənfi ola bilməz")]
    public decimal? AdditionalExpenseAmount { get; init; }

    /// <summary>
    /// Əlavə xərcin valyutası (USD və ya RUB).
    /// </summary>
    public PaymentCurrency? AdditionalExpenseCurrency { get; init; }

    /// <summary>
    /// Əlavə xərcin təsviri.
    /// </summary>
    public string? AdditionalExpenseDescription { get; init; }

    /// <summary>
    /// Local file path selected via OpenFileDialog (replaces MultipartFile from Java).
    /// </summary>
    public string? DocumentFilePath { get; init; }

    // === Kassadan Ödəniş ===

    /// <summary>
    /// Kassadan ödənişdə USD kassasından çıxılan məbləğ.
    /// </summary>
    public decimal? PaidFromUsdAmount { get; init; }

    /// <summary>
    /// Kassadan ödənişdə RUB kassasından çıxılan məbləğ.
    /// </summary>
    public decimal? PaidFromRubAmount { get; init; }

    // === Ödəniş Statusu və Ton Başına Qazanc ===

    /// <summary>
    /// Ödəniş statusu: Paid (Ödənilib), Unpaid (Ödənilməyib), PaidFromBalance (Kassadan Ödənilsin).
    /// </summary>
    public PaymentStatus PaymentStatus { get; init; } = PaymentStatus.Paid;

    /// <summary>
    /// Şirkətin ton başına qazancı.
    /// </summary>
    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Ton başına qazanc mənfi ola bilməz")]
    public decimal ProfitPerTon { get; init; }

    /// <summary>
    /// Ton başına qazancın valyutası (USD / RUB).
    /// </summary>
    public PaymentCurrency ProfitPerTonCurrency { get; init; } = PaymentCurrency.Usd;

    // === Göndərmə Statusu və Tarixlər ===
    public ShipmentStatus ShipmentStatus { get; init; } = ShipmentStatus.Pending;
    public DateOnly? LoadedDate { get; init; }
    public DateOnly? InTransitStartDate { get; init; }
    public DateOnly? InTransitEndDate { get; init; }
    public DateOnly? DeliveredDate { get; init; }
    public bool IsInTransitAutoDates { get; init; } = true;
}

/// <summary>
/// Request model specifically for updating the shipment tracking status of a transaction.
/// </summary>
public record ShipmentStatusUpdateRequest
{
    [Required(ErrorMessage = "Status seçilməlidir")]
    public ShipmentStatus Status { get; init; }

    public DateOnly? LoadedDate { get; init; }
    public DateOnly? InTransitStartDate { get; init; }
    public DateOnly? DeliveredDate { get; init; }
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
    public string? SendingCompany { get; init; }
    public decimal WeightTon { get; init; }
    public decimal PricePerTonRub { get; init; }
    public TransportType TransportType { get; init; }
    public PaymentCurrency TransportCurrency { get; init; }
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

    // === Ödəniş Statusu və Qazanc ===
    public PaymentStatus PaymentStatus { get; init; }
    public decimal ProfitPerTon { get; init; }
    public PaymentCurrency ProfitPerTonCurrency { get; init; }
    public decimal HistoricalUserProfitUsd { get; init; }
    public decimal HistoricalCustomerBilledUsd { get; init; }
    public decimal HistoricalBalanceDeltaUsd { get; init; }

    // === Kassadan Ödəniş ===
    public decimal? PaidFromUsdAmount { get; init; }
    public decimal? PaidFromRubAmount { get; init; }

    public string PaymentStatusDisplay => PaymentStatus switch
    {
        PaymentStatus.Paid => "Ödənilib",
        PaymentStatus.Unpaid => "Ödənilməyib",
        PaymentStatus.PaidFromBalance => "Kassadan Ödənilib",
        _ => PaymentStatus.ToString()
    };

    // === UI Formatlanmış Dəyərlər (Xərc / Qazanc üslubunda) ===
    public string PaidAmountFormatted => PaidAmount.HasValue
        ? $"{PaidAmount.Value:N0}{(PaidCurrency == PaymentCurrency.Rub ? "₽" : "$")}"
        : "0$";

    public string PricePerVehicleFormatted => PricePerVehicle.HasValue && PricePerVehicle.Value > 0
        ? $"{PricePerVehicle.Value:N0}{(TransportCurrency == PaymentCurrency.Rub ? "₽" : "$")}"
        : "—";

    public string AdditionalExpenseFormatted => AdditionalExpenseAmount.HasValue && AdditionalExpenseAmount.Value > 0
        ? $"{AdditionalExpenseAmount.Value:N0}{(AdditionalExpenseCurrency == PaymentCurrency.Rub ? "₽" : "$")}"
        : "—";

    public string PricePerTonRubFormatted => $"{PricePerTonRub:N0} ₽";

    public string WeightTonFormatted => $"{WeightTon:N2} T";

    public string BilledUsdFormatted => $"${HistoricalCustomerBilledUsd:N2}";
    public string PaidUsdFormatted => $"${PaidInUsd:N2}";

    // === Göndərmə Statusu və Tarixlər ===
    public ShipmentStatus ShipmentStatus { get; init; } = ShipmentStatus.Pending;
    public DateOnly? LoadedDate { get; init; }
    public DateOnly? InTransitStartDate { get; init; }
    public DateOnly? InTransitEndDate { get; init; }
    public DateOnly? DeliveredDate { get; init; }
    public bool IsInTransitAutoDates { get; init; } = true;

    public string ShipmentStatusDisplay => ShipmentStatus switch
    {
        ShipmentStatus.Pending => "Gözləmədə",
        ShipmentStatus.Loaded => "Yükləndi",
        ShipmentStatus.InTransit => "Yoldadır",
        ShipmentStatus.Delivered => "Çatdı",
        _ => ShipmentStatus.ToString()
    };

    public string TrackingDatesSummary
    {
        get
        {
            return ShipmentStatus switch
            {
                ShipmentStatus.Pending => string.Empty,
                ShipmentStatus.Loaded => LoadedDate.HasValue ? $"Yükləndi: {LoadedDate.Value:yyyy-MM-dd}" : string.Empty,
                ShipmentStatus.InTransit => InTransitStartDate.HasValue 
                    ? (LoadedDate.HasValue 
                        ? $"Yükləndi: {LoadedDate.Value:yyyy-MM-dd} | Yola çıxdı: {InTransitStartDate.Value:yyyy-MM-dd}" 
                        : $"Yola çıxdı: {InTransitStartDate.Value:yyyy-MM-dd}")
                    : string.Empty,
                ShipmentStatus.Delivered => DeliveredDate.HasValue
                    ? (InTransitStartDate.HasValue 
                        ? $"Çatdı: {DeliveredDate.Value:yyyy-MM-dd} | Yolda: {InTransitStartDate.Value:yyyy-MM-dd} — {DeliveredDate.Value:yyyy-MM-dd}"
                        : $"Çatdı: {DeliveredDate.Value:yyyy-MM-dd}")
                    : string.Empty,
                _ => string.Empty
            };
        }
    }

    // === Əlavə Xərclər ===
    public decimal? AdditionalExpenseAmount { get; init; }
    public PaymentCurrency? AdditionalExpenseCurrency { get; init; }
    public string? AdditionalExpenseDescription { get; init; }
    public decimal AdditionalExpenseUsd { get; init; }
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
    public string? SendingCompany { get; init; }
    public decimal WeightTon { get; init; }
    public decimal PricePerTonRub { get; init; }
    public TransportType TransportType { get; init; }
    public PaymentCurrency TransportCurrency { get; init; }
    public int? VehicleCount { get; init; }
    public decimal? PricePerVehicle { get; init; }
    public PaymentCurrency PaidCurrency { get; init; }
    public decimal? PaidAmount { get; init; }
    public decimal HistoricalExchangeRate { get; init; }
    public string? DocumentImageUrl { get; init; }
    public bool IsCompleted { get; init; }

    // === Ödəniş Statusu və Qazanc ===
    public PaymentStatus PaymentStatus { get; init; } = PaymentStatus.Paid;
    public decimal ProfitPerTon { get; init; }
    public PaymentCurrency ProfitPerTonCurrency { get; init; } = PaymentCurrency.Usd;

    // === Kassadan Ödəniş ===
    public decimal? PaidFromUsdAmount { get; init; }
    public decimal? PaidFromRubAmount { get; init; }

    // === Göndərmə Statusu və Tarixlər ===
    public ShipmentStatus ShipmentStatus { get; init; } = ShipmentStatus.Pending;
    public DateOnly? LoadedDate { get; init; }
    public DateOnly? InTransitStartDate { get; init; }
    public DateOnly? InTransitEndDate { get; init; }
    public DateOnly? DeliveredDate { get; init; }
    public bool IsInTransitAutoDates { get; init; } = true;

    // === Əlavə Xərclər ===
    public decimal? AdditionalExpenseAmount { get; init; }
    public PaymentCurrency? AdditionalExpenseCurrency { get; init; }
    public string? AdditionalExpenseDescription { get; init; }
}

/// <summary>
/// Financial summary report for a date range or customer.
/// Contains comprehensive financial metrics for the updated accounting engine.
/// </summary>
public record ExpenseIncomeReport
{
    /// <summary>Ümumi Xərc: Şirkətin çəkdiyi birbaşa xərclər (Maya + Daşıma + Əlavə xərc)</summary>
    public decimal TotalExpenseUsd { get; init; }

    /// <summary>Xərc + Qazanc: Müştəriyə hesablanan yekun məbləğ (TotalExpenseUsd + TotalUserProfitUsd)</summary>
    public decimal TotalBilledUsd { get; init; }

    /// <summary>Şirkət Qazancı / Mənfəəti: Ton başına qazanc * Çəki (USD)</summary>
    public decimal TotalUserProfitUsd { get; init; }

    /// <summary>Ümumi Ödəniş: Faktiki yığılmış ödənişlər (USD)</summary>
    public decimal TotalPaidUsd { get; init; }

    /// <summary>Qalıq Balans Fərqi: TotalPaidUsd - TotalBilledUsd (mənfi = borc var)</summary>
    public decimal TotalBenefitUsd { get; init; }

    /// <summary>Kassa Fərqi: TotalPaidUsd - TotalExpenseUsd (faktiki daxil olan pul - çıxan birbaşa xərc)</summary>
    public decimal TotalCashFlowUsd { get; init; }

    /// <summary>Müştəri Borcu: TotalBilledUsd > TotalPaidUsd olarsa fərq</summary>
    public decimal RemainingDebtUsd => TotalBilledUsd > TotalPaidUsd ? TotalBilledUsd - TotalPaidUsd : 0m;

    /// <summary>Ümumi tranzaksiya sayı</summary>
    public long TransactionCount { get; init; }

    /// <summary>Ödənilmiş tranzaksiyaların sayı</summary>
    public int PaidTransactionCount { get; init; }

    /// <summary>Ödənilməmiş tranzaksiyaların sayı</summary>
    public int UnpaidTransactionCount { get; init; }

    /// <summary>Cəmi yük çəkisi (ton)</summary>
    public decimal TotalWeightTon { get; init; }

    /// <summary>Cəmi nəqliyyat vasitəsi sayı</summary>
    public int TotalVehicleCount { get; init; }

    /// <summary>Mənfəət faizi (%): (TotalUserProfitUsd / TotalBilledUsd) * 100</summary>
    public decimal ProfitMarginPercent => TotalBilledUsd > 0
        ? Math.Round((TotalUserProfitUsd / TotalBilledUsd) * 100m, 1)
        : 0m;

    /// <summary>Ödəniş yığım faizi (%): (TotalPaidUsd / TotalBilledUsd) * 100</summary>
    public decimal CollectionRatePercent => TotalBilledUsd > 0
        ? Math.Round((TotalPaidUsd / TotalBilledUsd) * 100m, 1)
        : 0m;
}
