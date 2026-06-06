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

    [MaxLength(500)]
    public string? ReceivingCompany { get; set; }

    /// <summary>
    /// Weight in tons.
    /// </summary>
    public decimal WeightTon { get; set; }

    /// <summary>
    /// Price per ton in Russian Rubles.
    /// </summary>
    public decimal PricePerTonRub { get; set; }

    /// <summary>
    /// Transport type: Truck (USD pricing) or Ship (RUB pricing).
    /// Stored as string in SQLite.
    /// </summary>
    public TransportType TransportType { get; set; }

    /// <summary>
    /// Number of vehicles/ships used.
    /// </summary>
    public int? VehicleCount { get; set; }

    /// <summary>
    /// Cost per vehicle. USD for Truck, RUB for Ship.
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

    /// <summary>
    /// CALCULATED: Total cost in USD (goods + transport), computed at persist time.
    /// Formula: GoodsCostUsd + TransportCostUsd
    /// </summary>
    public decimal HistoricalTotalExpenseUsd { get; set; }

    /// <summary>
    /// CALCULATED: Remaining debt in USD, computed at persist time.
    /// Formula: PaidInUsd - HistoricalTotalExpenseUsd
    /// Negative = debt to supplier, Positive = overpayment.
    /// </summary>
    public decimal HistoricalRemainingDebtUsd { get; set; }

    /// <summary>
    /// Marks the transaction as completed.
    /// </summary>
    public bool IsCompleted { get; set; }

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
