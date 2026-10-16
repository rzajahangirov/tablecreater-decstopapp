using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TableCreater.WPF.Enums;

namespace TableCreater.WPF.Data.Entities;

/// <summary>
/// Tranzaksiyaya bağlı dinamik əlavə xərc (ton başına və ya vahid).
/// Hər tranzaksiyada istənilən sayda əlavə xərc ola bilər.
/// PerTon: Total = WeightTon * AmountPerTon.
/// Fixed: Total = AmountPerTon (vahid sabit məbləğ).
/// </summary>
[Table("TransactionAdditionalExpenses")]
public class TransactionAdditionalExpense
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// Əlaqəli tranzaksiyanın ID-si.
    /// </summary>
    [Required]
    public long TransactionId { get; set; }

    /// <summary>
    /// Əlavə xərcin başlığı / təsviri (məs: "Gömrük rüsumu", "Sığorta", "Anbar xərci").
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Əlavə xərcin hesablama növü: PerTon (Ton Başına) və ya Fixed (Vahid).
    /// </summary>
    public AdditionalExpenseType ExpenseType { get; set; } = AdditionalExpenseType.PerTon;

    /// <summary>
    /// Əlavə xərc məbləği.
    /// PerTon olduqda: ton başına məbləğ.
    /// Fixed olduqda: vahid (sabit) məbləğ.
    /// </summary>
    public decimal AmountPerTon { get; set; }

    /// <summary>
    /// Əlavə xərcin valyutası (USD və ya RUB).
    /// </summary>
    public PaymentCurrency Currency { get; set; } = PaymentCurrency.Usd;

    /// <summary>
    /// Hesablama növünə uyğun ümumi məbləğ.
    /// </summary>
    [NotMapped]
    public decimal TotalAmount => ExpenseType == AdditionalExpenseType.Fixed
        ? AmountPerTon
        : (Transaction?.WeightTon ?? 0m) * AmountPerTon;

    /// <summary>
    /// Əlavə xərcin hesablama növü üçün köməkçi xassə.
    /// </summary>
    [NotMapped]
    public AdditionalExpenseType Type
    {
        get => ExpenseType;
        set => ExpenseType = value;
    }

    // === Navigation Property ===

    [ForeignKey(nameof(TransactionId))]
    public Transaction Transaction { get; set; } = null!;
}
