using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TableCreater.WPF.Enums;

namespace TableCreater.WPF.Data.Entities;

/// <summary>
/// Defines a custom (dynamic) column that can be added to transactions.
/// Supports both manual entry and calculated (formula-based) columns.
/// </summary>
[Table("CustomColumns")]
public class CustomColumn
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the column value is entered manually or computed via a formula.
    /// Stored as string in SQLite.
    /// </summary>
    public ColumnInputType InputType { get; set; }

    /// <summary>
    /// Formula expression for calculated columns (null for manual columns).
    /// </summary>
    [MaxLength(1000)]
    public string? Formula { get; set; }

    /// <summary>
    /// The data type of this column (Text, Number, Money, Date, Image).
    /// Stored as string in SQLite.
    /// </summary>
    public Enums.DataType DataType { get; set; }

    /// <summary>
    /// Display order for this column in the UI.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Navigation property: all field values stored for this column definition.
    /// </summary>
    public ICollection<CustomFieldValue> CustomFieldValues { get; set; } = new List<CustomFieldValue>();
}
