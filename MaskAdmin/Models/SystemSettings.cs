using System.ComponentModel.DataAnnotations;

namespace MaskAdmin.Models;

public class SystemSettings
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    [MaxLength(50)]
    public string Category { get; set; } = "General";

    public string? Description { get; set; }

    public bool IsEncrypted { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
