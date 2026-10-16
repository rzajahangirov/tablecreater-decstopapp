using System.ComponentModel.DataAnnotations;
using TableCreater.WPF.Enums;
using TableCreater.WPF.Data.Entities;

namespace TableCreater.WPF.Models;

// ============================================================================
// TRANSACTION DTOs / Records
// Updated for dual-stream billing (RUB + USD independently) with dynamic
// additional expenses (per-ton, multiple items).
// ============================================================================

/// <summary>
/// DTO for a single additional expense item (per-ton or fixed).
/// </summary>
public record TransactionAdditionalExpenseDto
{
    public string Title { get; init; } = string.Empty;
    public AdditionalExpenseType Type { get; init; } = AdditionalExpenseType.PerTon;
    public decimal AmountPerTon { get; init; }
    public PaymentCurrency Currency { get; init; } = PaymentCurrency.Usd;
    public decimal TotalAmount { get; init; }
}

/// <summary>
/// Request model for creating a new transaction.
/// Updated: No more PaymentStatus, PaidAmount, exchange rate input.
/// Dual-stream billing with dynamic additional expenses.
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
    /// Nəqliyyat valyutası — manual seçilir.
    /// </summary>
    [Required(ErrorMessage = "Nəqliyyat valyutası seçilməlidir")]
    public PaymentCurrency TransportCurrency { get; init; } = PaymentCurrency.Usd;

    [Range(1, int.MaxValue, ErrorMessage = "Vasitə sayı ən az 1 olmalıdır")]
    public int? VehicleCount { get; init; }

    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Nəqliyyat qiyməti mənfi ola bilməz")]
    public decimal? PricePerVehicle { get; init; }

    // === Dinamik Əlavə Xərclər (ton başına, çox saylı) ===
    public List<TransactionAdditionalExpenseDto> AdditionalExpenses { get; init; } = new();

    /// <summary>
    /// Local file path selected via OpenFileDialog.
    /// </summary>
    public string? DocumentFilePath { get; init; }

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

    // === Legacy fields (kept for backward compatibility, defaults used) ===
    public decimal HistoricalExchangeRate { get; init; } = 1.0m;
    public PaymentCurrency PaidCurrency { get; init; } = PaymentCurrency.Usd;
    public decimal? PaidAmount { get; init; }
    public PaymentStatus PaymentStatus { get; init; } = PaymentStatus.Unpaid;
    public decimal? PaidFromUsdAmount { get; init; }
    public decimal? PaidFromRubAmount { get; init; }
    public decimal? AdditionalExpenseAmount { get; init; }
    public PaymentCurrency? AdditionalExpenseCurrency { get; init; }
    public string? AdditionalExpenseDescription { get; init; }
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
/// </summary>
public record TransactionUpdateRequest : TransactionCreateRequest
{
    public bool? IsCompleted { get; init; }
}

/// <summary>
/// Response model returned when reading a transaction.
/// Includes dual-stream billing totals and dynamic additional expenses.
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

    // === İkili Valyuta Hesablaşması ===
    public decimal TotalBilledRub { get; init; }
    public decimal TotalBilledUsd { get; init; }

    public string PaymentStatusDisplay => PaymentStatus switch
    {
        PaymentStatus.Paid => "Ödənilib",
        PaymentStatus.Unpaid => "Ödənilməyib",
        PaymentStatus.PaidFromBalance => "Kassadan Ödənilib",
        _ => PaymentStatus.ToString()
    };

    // === UI Formatlanmış Dəyərlər ===
    public string PaidAmountFormatted => PaidAmount.HasValue
        ? $"{PaidAmount.Value:N0}{(PaidCurrency == PaymentCurrency.Rub ? "₽" : "$")}"
        : "0$";

    public string PricePerVehicleFormatted => PricePerVehicle.HasValue && PricePerVehicle.Value > 0
        ? $"{PricePerVehicle.Value:N0}{(TransportCurrency == PaymentCurrency.Rub ? "₽" : "$")}"
        : "—";

    public string PricePerTonRubFormatted => $"{PricePerTonRub:N0} ₽";
    public string WeightTonFormatted => $"{WeightTon:N2} T";

    public string TotalBilledRubFormatted => $"{TotalBilledRub:N2} ₽";
    public string TotalBilledUsdFormatted => $"${TotalBilledUsd:N2}";
    public string BilledUsdFormatted => $"${HistoricalCustomerBilledUsd:N2}";
    public string PaidUsdFormatted => $"${PaidInUsd:N2}";

    // === Məhsul, Nəqliyyat və Qazanc Hesablamaları ===
    /// <summary>
    /// Malın Ümumi Dəyəri (RUB): Çəki * Ton Başı Qiymət
    /// </summary>
    public decimal GoodsTotalCostRub => WeightTon * PricePerTonRub;
    public string GoodsTotalCostRubFormatted => $"{GoodsTotalCostRub:N2} ₽";
    public string GoodsTotalCostFormula => $"{WeightTon:N2} T × {PricePerTonRub:N0} ₽";

    /// <summary>
    /// Cəmi Nəqliyyat Xərci: Sayı * Bir Vasitənin Qiyməti
    /// </summary>
    public decimal TransportTotalCost => (VehicleCount ?? 0) * (PricePerVehicle ?? 0m);
    public string TransportTotalCostFormatted => $"{TransportTotalCost:N2}{(TransportCurrency == PaymentCurrency.Rub ? " ₽" : "$")}";

    /// <summary>
    /// Hesablanmış Ümumi Şirkət Qazancı: Çəki * Ton Başı Qazanc
    /// </summary>
    public decimal TotalProfitCalculated => WeightTon * ProfitPerTon;
    public string TotalProfitCalculatedFormatted => $"{TotalProfitCalculated:N2}{(ProfitPerTonCurrency == PaymentCurrency.Rub ? " ₽" : "$")}";
    public string ProfitFormula => $"{WeightTon:N2} T × {ProfitPerTon:N2}{(ProfitPerTonCurrency == PaymentCurrency.Rub ? " ₽" : "$")}";

    // === Vahid Maliyyə (USD Ekvivalenti) ===
    /// <summary>
    /// Vahid Ümumi Xərc (Müştəri Hesabı): USD hesablanan + (RUB hesablanan / Məzənnə).
    /// </summary>
    public decimal TotalExpenseUnifiedUsd => TotalBilledUsd + (HistoricalExchangeRate > 0 ? TotalBilledRub / HistoricalExchangeRate : 0m);
    public string TotalExpenseUnifiedUsdFormatted => $"${TotalExpenseUnifiedUsd:N2}";

    /// <summary>
    /// Şirkət Qazancı (USD): Vahid gəlir / profit (USD ekvivalenti).
    /// </summary>
    public decimal ProfitUnifiedUsd => HistoricalUserProfitUsd;
    public string ProfitUnifiedUsdFormatted => $"${ProfitUnifiedUsd:N2}";

    /// <summary>
    /// Xalis Maya Dəyəri (USD): Ümumi Xərc - Qazanc.
    /// </summary>
    public decimal BaseCostUnifiedUsd => Math.Max(0m, TotalExpenseUnifiedUsd - ProfitUnifiedUsd);
    public string BaseCostUnifiedUsdFormatted => $"${BaseCostUnifiedUsd:N2}";

    /// <summary>
    /// Vahid Əlavə Xərclər Cəmi (USD): Məzənnəyə uyğun olaraq USD ekvivalenti.
    /// </summary>
    public decimal AdditionalExpensesUnifiedUsd => AdditionalExpensesUsdTotal + (HistoricalExchangeRate > 0 ? AdditionalExpensesRubTotal / HistoricalExchangeRate : 0m);
    public string AdditionalExpensesUnifiedUsdFormatted => $"${AdditionalExpensesUnifiedUsd:N2}";

    public string HistoricalExchangeRateFormatted => HistoricalExchangeRate > 0 ? $"{HistoricalExchangeRate:N2} ₽" : "—";

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

    // === Dinamik Əlavə Xərclər ===
    public List<TransactionAdditionalExpenseDto> AdditionalExpenses { get; init; } = new();
    public decimal AdditionalExpensesRubTotal { get; init; }
    public decimal AdditionalExpensesUsdTotal { get; init; }

    // Legacy
    public decimal? AdditionalExpenseAmount { get; init; }
    public PaymentCurrency? AdditionalExpenseCurrency { get; init; }
    public decimal AdditionalExpenseUsd { get; init; }

    public string AdditionalExpenseFormatted
    {
        get
        {
            if (AdditionalExpenses != null && AdditionalExpenses.Count > 0)
            {
                var rub = AdditionalExpensesRubTotal;
                var usd = AdditionalExpensesUsdTotal;

                if (rub > 0 && usd > 0)
                {
                    decimal totalUsd = usd + (HistoricalExchangeRate > 0 ? rub / HistoricalExchangeRate : 0m);
                    return $"${usd:N0} + {rub:N0}₽ (${totalUsd:N2})";
                }
                if (rub > 0)
                {
                    decimal totalUsd = HistoricalExchangeRate > 0 ? rub / HistoricalExchangeRate : 0m;
                    return totalUsd > 0 ? $"{rub:N0}₽ (${totalUsd:N2})" : $"{rub:N0}₽";
                }
                if (usd > 0)
                {
                    return $"${usd:N0}";
                }
            }

            if (AdditionalExpenseAmount.HasValue && AdditionalExpenseAmount.Value > 0)
            {
                if (AdditionalExpenseCurrency == PaymentCurrency.Rub)
                {
                    decimal totalUsd = HistoricalExchangeRate > 0 ? AdditionalExpenseAmount.Value / HistoricalExchangeRate : 0m;
                    return totalUsd > 0 ? $"{AdditionalExpenseAmount.Value:N0}₽ (${totalUsd:N2})" : $"{AdditionalExpenseAmount.Value:N0}₽";
                }
                return $"${AdditionalExpenseAmount.Value:N0}";
            }

            return "—";
        }
    }

    public string AdditionalExpenseDescription
    {
        get
        {
            if (AdditionalExpenses != null && AdditionalExpenses.Count > 0)
            {
                var lines = AdditionalExpenses.Select(e =>
                {
                    string sym = e.Currency == PaymentCurrency.Rub ? "₽" : "$";
                    if (e.Type == AdditionalExpenseType.Fixed)
                    {
                        return $"{e.Title} (Vahid): {e.AmountPerTon:N2}{sym}";
                    }
                    else
                    {
                        decimal total = e.TotalAmount > 0 ? e.TotalAmount : (WeightTon * e.AmountPerTon);
                        return $"{e.Title} (Ton Başı): {WeightTon:N2}T × {e.AmountPerTon:N2}{sym} = {total:N2}{sym}";
                    }
                }).ToList();

                if (HistoricalExchangeRate > 0 && AdditionalExpensesRubTotal > 0)
                {
                    decimal totalUsd = AdditionalExpensesUsdTotal + (AdditionalExpensesRubTotal / HistoricalExchangeRate);
                    lines.Add("---");
                    lines.Add($"Cəmi (USD Məzənnə ilə): ${totalUsd:N2}");
                }

                return string.Join(Environment.NewLine, lines);
            }

            if (!string.IsNullOrWhiteSpace(AdditionalExpenseDescriptionOriginal))
                return AdditionalExpenseDescriptionOriginal;

            return "Əlavə xərc yoxdur";
        }
        init => AdditionalExpenseDescriptionOriginal = value;
    }

    private string? AdditionalExpenseDescriptionOriginal { get; init; }
}

/// <summary>
/// Response model for populating the transaction edit form.
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
    public PaymentStatus PaymentStatus { get; init; } = PaymentStatus.Unpaid;
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

    // === Dinamik Əlavə Xərclər ===
    public List<TransactionAdditionalExpenseDto> AdditionalExpenses { get; init; } = new();

    // Legacy single expense (for old data)
    public decimal? AdditionalExpenseAmount { get; init; }
    public PaymentCurrency? AdditionalExpenseCurrency { get; init; }
    public string? AdditionalExpenseDescription { get; init; }
}

/// <summary>
/// Financial summary report for a date range or customer.
/// Updated with dual-stream (RUB + USD) totals.
/// </summary>
public record ExpenseIncomeReport
{
    /// <summary>Ümumi Xərc: Şirkətin çəkdiyi birbaşa xərclər (Maya + Daşıma + Əlavə xərc) — USD ekvivalenti</summary>
    public decimal TotalExpenseUsd { get; init; }

    /// <summary>Ümumi Xərc (Müştəri Hesabı): Müştəriyə hesablanan yekun məbləğ (USD ekvivalenti)</summary>
    public decimal TotalBilledUsd { get; init; }

    /// <summary>Şirkət Qazancı / Mənfəəti: Ton başına qazanc * Çəki (USD)</summary>
    public decimal TotalUserProfitUsd { get; init; }

    /// <summary>Ümumi Ödəniş: Faktiki yığılmış ödənişlər (USD)</summary>
    public decimal TotalPaidUsd { get; init; }

    /// <summary>Qalıq Balans Fərqi: TotalPaidUsd - TotalBilledUsd (mənfi = borc var)</summary>
    public decimal TotalBenefitUsd { get; init; }

    /// <summary>Kassa Fərqi: TotalPaidUsd - TotalExpenseUsd</summary>
    public decimal TotalCashFlowUsd { get; init; }

    /// <summary>Müştəri Borcu</summary>
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

    /// <summary>Mənfəət faizi (%)</summary>
    public decimal ProfitMarginPercent => TotalBilledUsd > 0
        ? Math.Round((TotalUserProfitUsd / TotalBilledUsd) * 100m, 1)
        : 0m;

    /// <summary>Ödəniş yığım faizi (%)</summary>
    public decimal CollectionRatePercent => TotalBilledUsd > 0
        ? Math.Round((TotalPaidUsd / TotalBilledUsd) * 100m, 1)
        : 0m;

    // === İkili Valyuta Cəmləri ===
    /// <summary>Cəmi RUB hesablaşması (bütün tranzaksiyalar)</summary>
    public decimal TotalBilledRubSum { get; init; }

    /// <summary>Cəmi USD hesablaşması (bütün tranzaksiyalar)</summary>
    public decimal TotalBilledUsdSum { get; init; }
}
