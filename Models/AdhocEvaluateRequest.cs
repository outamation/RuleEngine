namespace DemoRuleEngine.Models;

public class AdhocEvaluateRequest
{
    public string Expression { get; set; } = string.Empty;
    public string SampleJson { get; set; } = string.Empty;
    public int? WorkflowId { get; set; }
    public int? RuleId { get; set; }
}
