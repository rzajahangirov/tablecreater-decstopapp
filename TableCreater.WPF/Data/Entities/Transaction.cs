using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TableCreater.WPF.Enums;

namespace TableCreater.WPF.Data.Entities;

/// <summary>
/// Represents a financial transaction linked to a customer.
/// Contains historical financial data calculated at creation/update time.
/// Mapped from Java Transaction entity with @PrePersist/@PreUpdate calculations.
/// </summary>
[Table("Transactions")]
public class Transaction
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// Foreign key to the parent Customer. CASCADE DELETE configured in DbContext.
    /// </summary>
    [Required]
    public long CustomerId { get; set; }

    /// <summary>
    /// User-selected transaction date.
    /// </summary>
    [Required]
    public DateOnly TransactionDate { get; set; }

    /// <summary>
    /// System-generated creation timestamp (set automatically on persist).
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? ProductName { get; set; }

    /// <summary>
    /// Qəbul edən firma (receiving company).
    /// </summary>
    [MaxLength(500)]
    public string? ReceivingCompany { get; set; }

    /// <summary>
    /// Göndərən firma (sending company).
    /// </summary>
    [MaxLength(500)]
    public string? SendingCompany { get; set; }

    /// <summary>
    /// Weight in tons.
    /// </summary>
    public decimal WeightTon { get; set; }

    /// <summary>
    /// Price per ton in Russian Rubles.
    /// </summary>
    public decimal PricePerTonRub { get; set; }

    /// <summary>
    /// Transport type: Truck or Wagon.
    /// Stored as string in SQLite.
    /// </summary>
    public TransportType TransportType { get; set; }

    /// <summary>
    /// Currency for transport pricing. Manual selection (USD or RUB).
    /// No longer auto-determined by TransportType.
    /// </summary>
    public PaymentCurrency TransportCurrency { get; set; } = PaymentCurrency.Usd;

    /// <summary>
    /// Number of vehicles/wagons used.
    /// </summary>
    public int? VehicleCount { get; set; }

    /// <summary>
    /// Cost per vehicle. Currency is determined by TransportCurrency.
    /// </summary>
    public decimal? PricePerVehicle { get; set; }

    /// <summary>
    /// Amount paid by the customer.
    /// </summary>
    public decimal? PaidAmount { get; set; }

    /// <summary>
    /// Currency of payment. When RUB, must be converted to USD via HistoricalExchangeRate.
    /// Stored as string in SQLite.
    /// </summary>
    public PaymentCurrency PaidCurrency { get; set; }

    /// <summary>
    /// Path to the attached document file (e.g., PDF/JPG).
    /// </summary>
    [MaxLength(1000)]
    public string? DocumentPath { get; set; }

    /// <summary>
    /// RUB-to-USD exchange rate locked at the time of transaction creation.
    /// </summary>
    [Required]
    public decimal HistoricalExchangeRate { get; set; }

    // === Əlavə Xərclər (Additional Expenses) ===

    /// <summary>
    /// Əlavə xərc məbləği. Nullable — mütləq doldurulmalı deyil.
    /// </summary>
    public decimal? AdditionalExpenseAmount { get; set; }

    /// <summary>
    /// Əlavə xərcin valyutası (USD və ya RUB). Hesablamalarda məzənnəyə görə konvertasiya olunur.
    /// </summary>
    public PaymentCurrency? AdditionalExpenseCurrency { get; set; }

    /// <summary>
    /// Əlavə xərcin təsviri — nə üçün olduğunu açıqlayır.
    /// </summary>
    [MaxLength(1000)]
    public string? AdditionalExpenseDescription { get; set; }

    /// <summary>
    /// CALCULATED: Total cost in USD (goods + transport + additional expenses), computed at persist time.
    /// Formula: GoodsCostUsd + TransportCostUsd + AdditionalExpenseUsd
    /// </summary>
    public decimal HistoricalTotalExpenseUsd { get; set; }

    /// <summary>
    /// CALCULATED: Remaining debt in USD, computed at persist time.
    /// Formula: PaidInUsd - HistoricalTotalExpenseUsd
    /// Negative = debt to supplier, Positive = overpayment.
    /// </summary>
    public decimal HistoricalRemainingDebtUsd { get; set; }

    /// <summary>
    /// Marks the transaction as completed (synchronized with ShipmentStatus == Delivered).
    /// </summary>
    public bool IsCompleted { get; set; }

    // === Göndərmə Statusu və İzləmə (Shipment Tracking) ===

    /// <summary>
    /// Cari göndərmə statusu: Pending, Loaded, InTransit, Delivered.
    /// </summary>
    public ShipmentStatus ShipmentStatus { get; set; } = ShipmentStatus.Pending;

    /// <summary>
    /// Yükləndiyi tarix (Loaded statusunda tələb olunur).
    /// </summary>
    public DateOnly? LoadedDate { get; set; }

    /// <summary>
    /// Yola çıxdığı başlanğıc tarix.
    /// </summary>
    public DateOnly? InTransitStartDate { get; set; }

    /// <summary>
    /// Yolda olma bitmə tarixi (və ya çatdırılma tarixi ilə eyniləşdirilir).
    /// </summary>
    public DateOnly? InTransitEndDate { get; set; }

    /// <summary>
    /// Çatdığı tarix (Delivered statusunda tələb olunur).
    /// </summary>
    public DateOnly? DeliveredDate { get; set; }

    /// <summary>
    /// Yolda olma tarix aralığı avtomatik tenzimlenir (LoadedDate və DeliveredDate aralığı) yoxsa əl ilə daxil edilib.
    /// </summary>
    public bool IsInTransitAutoDates { get; set; } = true;

    // === Navigation Properties ===

    /// <summary>
    /// Navigation property to the parent Customer entity.
    /// </summary>
    [ForeignKey(nameof(CustomerId))]
    public Customer Customer { get; set; } = null!;

    /// <summary>
    /// Navigation property: custom field values for dynamic columns.
    /// </summary>
    public ICollection<CustomFieldValue> CustomFieldValues { get; set; } = new List<CustomFieldValue>();
}
