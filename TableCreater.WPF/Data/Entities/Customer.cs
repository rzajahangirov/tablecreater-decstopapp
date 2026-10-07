using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TableCreater.WPF.Enums;

namespace TableCreater.WPF.Data.Entities;

/// <summary>
/// Represents a customer in the system.
/// One-to-Many relationship with Transaction (CASCADE DELETE).
/// Mapped from Java Customer entity.
/// </summary>
[Table("Customers")]
public class Customer
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Phone { get; set; }

    /// <summary>
    /// Customer status: Active or Inactive. Default is Active.
    /// Stored as string in SQLite.
    /// </summary>
    public CustomerType Type { get; set; } = CustomerType.Active;

    /// <summary>
    /// Müştərinin əsas cari balansı (USD).
    /// Müsbət = müştərinin bizdə avansı/artığı var. Mənfi = müştərinin borcu var.
    /// </summary>
    public decimal BalanceUsd { get; set; } = 0m;

    /// <summary>
    /// Müştərinin Rubl balansı (RUB).
    /// Müsbət = avans, Mənfi = borc.
    /// </summary>
    public decimal BalanceRub { get; set; } = 0m;

    /// <summary>
    /// Navigation property: all transactions belonging to this customer.
    /// Configured with CASCADE DELETE in DbContext.
    /// </summary>
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    /// <summary>
    /// Navigation property: all balance audit transactions for this customer.
    /// </summary>
    public ICollection<CustomerBalanceHistory> BalanceHistories { get; set; } = new List<CustomerBalanceHistory>();
}
