namespace DemoRuleEngine.Models;

public class WorkflowDto
{
    public int Id { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ModifiedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
}
