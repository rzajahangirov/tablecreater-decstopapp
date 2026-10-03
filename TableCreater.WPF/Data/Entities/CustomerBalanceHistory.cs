using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TableCreater.WPF.Enums;

namespace TableCreater.WPF.Data.Entities;

/// <summary>
/// Müştərinin əsas balansı ilə bağlı bütün əməliyyatların tam audit tarixçəsi (Ledger).
/// </summary>
[Table("CustomerBalanceHistories")]
public class CustomerBalanceHistory
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// Əlaqəli müştərinin ID-si.
    /// </summary>
    public long CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    /// <summary>
    /// Əgər əməliyyat tranzaksiya ilə bağlıdırsa, həmin tranzaksiyanın ID-si (əks halda null).
    /// </summary>
    public long? TransactionId { get; set; }

    public Transaction? Transaction { get; set; }

    /// <summary>
    /// Əməliyyatın tarixi və vaxtı (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Balans əməliyyatının növü.
    /// </summary>
    public BalanceTransactionType Type { get; set; }

    /// <summary>
    /// Balansa edilən dəyişiklik məbləği (USD).
    /// Müsbət = balans artır, mənfi = balans azalır.
    /// </summary>
    public decimal AmountUsd { get; set; }

    /// <summary>
    /// Bu əməliyyatdan dərhal sonrakı müştərinin qalıq balansı (USD).
    /// </summary>
    public decimal BalanceAfterUsd { get; set; }

    /// <summary>
    /// Əməliyyat haqqında izahat və ya istifadəçi qeydi.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }
}
