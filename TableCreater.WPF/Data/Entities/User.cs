using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TableCreater.WPF.Data.Entities;

/// <summary>
/// Represents a user of the system.
/// Mapped from Java User entity with JPA annotations.
/// </summary>
[Table("Users")]
public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Surname { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Many-to-Many relationship with Role via UserRoles join table.
    /// </summary>
    public ICollection<Role> Roles { get; set; } = new List<Role>();
}
