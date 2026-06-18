using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using RulesEngine.Models;

namespace DemoRuleEngine.Models;

[Table("WorkflowRules")]
public class RuleEntity
{
    [Key]
    public int Id { get; set; }

    public int WorkflowId { get; set; }

    [ForeignKey(nameof(WorkflowId))]
    [JsonIgnore]
    public WorkflowEntity Workflow { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string RuleName { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public string? SampleJson { get; set; }

    public Rule Definition { get; set; } = null!;

    /// <summary>User Id of the creator.</summary>
    public int? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>User Id of the last modifier.</summary>
    public int? ModifiedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }
}
