using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TableCreater.WPF.Data.Entities;

/// <summary>
/// Stores the value of a custom (dynamic) column for a specific transaction.
/// Links a Transaction to a CustomColumn with a string-serialized value.
/// </summary>
[Table("CustomFieldValues")]
public class CustomFieldValue
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// Foreign key to the Transaction this value belongs to.
    /// </summary>
    [Required]
    public long TransactionId { get; set; }

    /// <summary>
    /// Foreign key to the CustomColumn definition.
    /// </summary>
    [Required]
    public long CustomColumnId { get; set; }

    /// <summary>
    /// The actual value, stored as a string regardless of the column's DataType.
    /// Interpretation depends on the associated CustomColumn.DataType.
    /// </summary>
    [MaxLength(2000)]
    public string? Value { get; set; }

    // === Navigation Properties ===

    [ForeignKey(nameof(TransactionId))]
    public Transaction Transaction { get; set; } = null!;

    [ForeignKey(nameof(CustomColumnId))]
    public CustomColumn CustomColumn { get; set; } = null!;
}
