using DemoRuleEngine.Data;
using DemoRuleEngine.Helpers;
using DemoRuleEngine.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RulesEngine.Models;


namespace DemoRuleEngine.Services;

public class RuleManagerService : IRuleManagerService
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly ReSettings _reSettings;
    private RulesEngine.RulesEngine _engine = null!;
    private readonly ILogger<RuleManagerService> _logger;
    private readonly IRuleAuditService _auditService;
    private readonly IDbContextFactory<RuleDbContext> _dbFactory;

    public RuleManagerService(
        ILogger<RuleManagerService> logger,
        IRuleAuditService auditService,
        IDbContextFactory<RuleDbContext> dbFactory)
    {
        _logger = logger;
        _auditService = auditService;
        _dbFactory = dbFactory;

        _reSettings = new ReSettings
        {
            CustomTypes = new[] { typeof(RuleHelper) }
        };

        _engine = new RulesEngine.RulesEngine(Array.Empty<Workflow>(), _reSettings);

        Task.Run(() =>
        {
            try { LoadRulesFromDatabase(); }
            catch (Exception ex) { _logger.LogError(ex, "Failed to load rules from database on startup."); }
        });
    }

    // ??? Engine bootstrap & hot-reload ??????????????????????????????????????

    private void LoadRulesFromDatabase()
    {
        _lock.EnterWriteLock();
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var dbWorkflows = db.Workflows.Include(w => w.Rules).ToList();
            RebuildEngineFromEntities(dbWorkflows);
            _logger.LogInformation("Rules loaded from database. Engine initialized.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading rules from database.");
            throw;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private static void SanitizeRuleExpressions(Rule rule)
    {
        if (rule == null) return;

        if (!string.IsNullOrWhiteSpace(rule.Expression))
        {
            rule.Expression = System.Text.RegularExpressions.Regex.Replace(
                rule.Expression, 
                @"\b([a-zA-Z_][a-zA-Z0-9_]*)-([a-zA-Z_][a-zA-Z0-9_]*)\b", 
                "$1_$2"
            );
        }

        if (rule.LocalParams != null)
        {
            foreach (var lp in rule.LocalParams)
            {
                if (!string.IsNullOrWhiteSpace(lp.Expression))
                {
                    lp.Expression = System.Text.RegularExpressions.Regex.Replace(
                        lp.Expression, 
                        @"\b([a-zA-Z_][a-zA-Z0-9_]*)-([a-zA-Z_][a-zA-Z0-9_]*)\b", 
                        "$1_$2"
                    );
                }
            }
        }

        if (rule.Rules != null)
        {
            foreach (var child in rule.Rules)
            {
                SanitizeRuleExpressions(child);
            }
        }
    }

    private void RebuildEngineFromEntities(List<WorkflowEntity> dbWorkflows)
    {
        var workflows = dbWorkflows.Select(w => new Workflow
        {
            WorkflowName = w.WorkflowName,
            GlobalParams = w.GlobalParamsJson != null
                ? JsonConvert.DeserializeObject<IEnumerable<ScopedParam>>(w.GlobalParamsJson)
                : null,
            Rules = w.Rules?.Select(r => 
            {
                var rule = r.ToRulesEngineRule();
                SanitizeRuleExpressions(rule);
                return rule;
            }).ToList()
        }).ToList();

        var json = JsonConvert.SerializeObject(workflows);
        var clone = JsonConvert.DeserializeObject<List<Workflow>>(json) ?? new List<Workflow>();

        _engine = new RulesEngine.RulesEngine(clone.ToArray(), _reSettings);
        _logger.LogInformation("Engine hot-reloaded successfully.");
    }

    private async Task ReloadEngineAsync()
    {
        using var db = _dbFactory.CreateDbContext();
        var dbWorkflows = await db.Workflows.Include(w => w.Rules).ToListAsync();
        _lock.EnterWriteLock();
        try { RebuildEngineFromEntities(dbWorkflows); }
        finally { _lock.ExitWriteLock(); }
    }

    public RulesEngine.RulesEngine GetEngine()
    {
        _lock.EnterReadLock();
        try { return _engine; }
        finally { _lock.ExitReadLock(); }
    }

    // ??? Workflows ????????????????????????????????????????????????????????

    public async Task<List<WorkflowDto>> GetWorkflowsAsync()
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.Workflows
            .Select(w => new WorkflowDto { Id = w.Id, WorkflowName = w.WorkflowName })
            .ToListAsync();
    }

    public async Task<WorkflowDto> CreateWorkflowAsync(string workflowName)
    {
        using var db = _dbFactory.CreateDbContext();

        if (await db.Workflows.AnyAsync(w => w.WorkflowName == workflowName))
            throw new InvalidOperationException($"Workflow '{workflowName}' already exists.");

        var entity = new WorkflowEntity { WorkflowName = workflowName };
        db.Workflows.Add(entity);
        await db.SaveChangesAsync();

        await _auditService.LogAsync("Workflow", workflowName, "Create", null, null, "Workflow created", "System");
        await ReloadEngineAsync();

        return new WorkflowDto { Id = entity.Id, WorkflowName = entity.WorkflowName };
    }

    public async Task DeleteWorkflowAsync(int workflowId, string? changedBy = null)
    {
        using var db = _dbFactory.CreateDbContext();

        var entity = await db.Workflows.FirstOrDefaultAsync(w => w.Id == workflowId);
        if (entity is null) return;

        var name = entity.WorkflowName;
        db.Workflows.Remove(entity);
        await db.SaveChangesAsync();

        await _auditService.LogAsync("Workflow", name, "Delete", null, null, "Workflow deleted", changedBy);
        await ReloadEngineAsync();
    }

    // ─── Rules

    public async Task<List<RuleEntity>> GetRulesAsync(int workflowId)
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.Rules.Where(r => r.WorkflowId == workflowId).ToListAsync();
    }

    public async Task<RuleEntity?> GetRuleAsync(int workflowId, int ruleId)
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.Rules.FirstOrDefaultAsync(r => r.WorkflowId == workflowId && r.Id == ruleId);
    }

    public async Task<RuleEntity?> GetRuleByNameAsync(int workflowId, string ruleName)
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.Rules.FirstOrDefaultAsync(r => r.WorkflowId == workflowId && r.RuleName == ruleName);
    }

    public async Task AddRuleFromDtoAsync(int workflowId, RuleDefinitionDto dto, string? changedBy = null)
    {
        using var db = _dbFactory.CreateDbContext();

        if (!await db.Workflows.AnyAsync(w => w.Id == workflowId))
            throw new InvalidOperationException($"Workflow ID {workflowId} does not exist.");

        var builtRule = RuleExpressionBuilder.Build(dto);

        var entity = new RuleEntity { WorkflowId = workflowId, SampleJson = dto.SampleJson };
        entity.UpdateFromRulesEngineRule(builtRule);

        db.Rules.Add(entity);
        await db.SaveChangesAsync();

        await _auditService.LogChangesAsync("Rule", dto.RuleName, null, entity, changedBy);
        await ReloadEngineAsync();
    }

    public async Task<RuleEntity> UpdateRuleAsync(int workflowId, int ruleId, RuleDefinitionDto dto, string? changedBy = null)
    {
        using var db = _dbFactory.CreateDbContext();

        var entity = await db.Rules.FirstOrDefaultAsync(r => r.WorkflowId == workflowId && r.Id == ruleId)
            ?? throw new KeyNotFoundException($"Rule ID {ruleId} not found in Workflow {workflowId}.");

        var oldClone = new RuleEntity
        {
            Id = entity.Id,
            WorkflowId = entity.WorkflowId,
            RuleName = entity.RuleName,
            Enabled = entity.Enabled,
            SampleJson = entity.SampleJson,
            Definition = new Rule
            {
                Expression = entity.Definition?.Expression,
                SuccessEvent = entity.Definition?.SuccessEvent,
                ErrorMessage = entity.Definition?.ErrorMessage,
                Enabled = entity.Definition?.Enabled ?? true,
                Operator = entity.Definition?.Operator,
                RuleExpressionType = entity.Definition?.RuleExpressionType ?? RuleExpressionType.LambdaExpression,
                WorkflowsToInject = entity.Definition?.WorkflowsToInject,
                LocalParams = entity.Definition?.LocalParams,
                Properties = entity.Definition?.Properties,
                Actions = entity.Definition?.Actions,
                Rules = entity.Definition?.Rules
            }
        };

        var builtRule = RuleExpressionBuilder.Build(dto);
        entity.UpdateFromRulesEngineRule(builtRule);
        entity.SampleJson = dto.SampleJson;

        await db.SaveChangesAsync();
        await _auditService.LogChangesAsync("Rule", dto.RuleName, oldClone, entity, changedBy);
        await ReloadEngineAsync();

        return entity;
    }

    public async Task ToggleRuleAsync(int workflowId, int ruleId, bool enabled, string? changedBy = null)
    {
        using var db = _dbFactory.CreateDbContext();

        var entity = await db.Rules.FirstOrDefaultAsync(r => r.WorkflowId == workflowId && r.Id == ruleId);
        if (entity is null || entity.Enabled == enabled) return;

        var oldVal = entity.Enabled.ToString();
        entity.Enabled = enabled;
        await db.SaveChangesAsync();

        await _auditService.LogAsync("Rule", entity.RuleName, "Update", "Enabled", oldVal, enabled.ToString(), changedBy);
        await ReloadEngineAsync();
    }

    public async Task DeleteRuleAsync(int workflowId, int ruleId, string? changedBy = null)
    {
        using var db = _dbFactory.CreateDbContext();

        var entity = await db.Rules.FirstOrDefaultAsync(r => r.WorkflowId == workflowId && r.Id == ruleId);
        if (entity is null) return;

        var ruleName = entity.RuleName;
        db.Rules.Remove(entity);
        await db.SaveChangesAsync();

        await _auditService.LogAsync("Rule", ruleName, "Delete", null, null, "Rule deleted", changedBy);
        await ReloadEngineAsync();
    }

    // ─── Helpers

    public async Task<string?> GetWorkflowNameAsync(int workflowId)
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.Workflows
            .Where(w => w.Id == workflowId)
            .Select(w => w.WorkflowName)
            .FirstOrDefaultAsync();
    }
}
