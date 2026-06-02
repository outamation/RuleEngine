using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoRuleEngine.Models;

[Table("RuleAuditLogs")]
public class RuleAuditLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string EntityName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? FieldName { get; set; }

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    [MaxLength(200)]
    public string? ChangedBy { get; set; }

    public DateTime ChangedDate { get; set; } = DateTime.UtcNow;
}
