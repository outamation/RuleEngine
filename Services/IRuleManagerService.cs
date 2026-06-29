using DemoRuleEngine.Models;
using DemoRuleEngine.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DemoRuleEngine.Services;

public interface IRuleManagerService
{
    RulesEngine.RulesEngine GetEngine();

    // Workflows
    Task<List<WorkflowDto>> GetWorkflowsAsync();
    Task<WorkflowDto> CreateWorkflowAsync(string workflowName, int? createdBy = null);
    Task<WorkflowDto> UpdateWorkflowAsync(int workflowId, string workflowName, int? modifiedBy = null);
    Task DeleteWorkflowAsync(int workflowId, int? changedById = null);

    // Rules
    Task<List<RuleEntity>> GetRulesAsync(int workflowId);
    Task<RuleEntity?> GetRuleAsync(int workflowId, int ruleId);
    Task<RuleEntity?> GetRuleByNameAsync(int workflowId, string ruleName);
    Task AddRuleFromDtoAsync(int workflowId, RuleDefinitionDto dto);
    Task<RuleEntity> UpdateRuleAsync(int workflowId, int ruleId, RuleDefinitionDto dto);
    Task ToggleRuleAsync(int workflowId, int ruleId, bool enabled, int? modifiedBy = null);
    Task DeleteRuleAsync(int workflowId, int ruleId, int? changedById = null);

    // Helpers
    Task<string?> GetWorkflowNameAsync(int workflowId);
}
