using RulesEngine.Models;
using System.Collections.Generic;

namespace DemoRuleEngine.Models;

public class RuleDefinitionDto
{
    public string RuleName { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SuccessMessage { get; set; }
    public string? FailureMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public List<LocalParamDto>? LocalParams { get; set; }
    public string? SampleJson { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Operator { get; set; }
    public List<string>? WorkflowsToInject { get; set; }
    public RuleActions? Actions { get; set; }
    public List<RuleDefinitionDto>? Rules { get; set; } // Recursive structure for nested rules

    /// <summary>User Id of the creator (sent by the caller on create).</summary>
    public int? CreatedBy { get; set; }

    /// <summary>User Id of the modifier (sent by the caller on update/toggle).</summary>
    public int? ModifiedBy { get; set; }
}
