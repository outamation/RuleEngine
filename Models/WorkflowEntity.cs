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

    public ICollection<RuleEntity> Rules { get; set; } = new List<RuleEntity>();
}
