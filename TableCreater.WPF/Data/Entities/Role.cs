using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TableCreater.WPF.Data.Entities;

/// <summary>
/// Represents a user role (e.g., ADMIN, USER).
/// Mapped from Java Role entity.
/// </summary>
[Table("Roles")]
public class Role
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Many-to-Many relationship with User via UserRoles join table.
    /// </summary>
    public ICollection<User> Users { get; set; } = new List<User>();
}
