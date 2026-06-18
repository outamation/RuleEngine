using System.ComponentModel.DataAnnotations;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoRuleEngine.Models;

[Table("Workflows")]
public class WorkflowEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string WorkflowName { get; set; } = string.Empty;

    public string? GlobalParamsJson { get; set; }

    /// <summary>User Id of the creator.</summary>
    public int? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>User Id of the last modifier.</summary>
    public int? ModifiedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public ICollection<RuleEntity> Rules { get; set; } = new List<RuleEntity>();
}
