using DemoRuleEngine.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DemoRuleEngine.Services;

public interface IRuleManagerService
{
    RulesEngine.RulesEngine GetEngine();

    // Workflows
    Task<List<WorkflowDto>> GetWorkflowsAsync();
    Task<WorkflowDto> CreateWorkflowAsync(string workflowName);
    Task DeleteWorkflowAsync(int workflowId, string? changedBy = null);

    // Rules
    Task<List<RuleEntity>> GetRulesAsync(int workflowId);
    Task<RuleEntity?> GetRuleAsync(int workflowId, int ruleId);
    Task<RuleEntity?> GetRuleByNameAsync(int workflowId, string ruleName);
    Task AddRuleFromDtoAsync(int workflowId, RuleDefinitionDto dto, string? changedBy = null);
    Task<RuleEntity> UpdateRuleAsync(int workflowId, int ruleId, RuleDefinitionDto dto, string? changedBy = null);
    Task ToggleRuleAsync(int workflowId, int ruleId, bool enabled, string? changedBy = null);
    Task DeleteRuleAsync(int workflowId, int ruleId, string? changedBy = null);

    // Helpers
    Task<string?> GetWorkflowNameAsync(int workflowId);
}
