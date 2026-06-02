using DemoRuleEngine.Models;
using DemoRuleEngine.Data;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DemoRuleEngine.Services;

public interface IRuleAuditService
{
    Task LogAsync(string entityType, string entityName, string action, string? fieldName, string? oldValue, string? newValue, string? changedBy);
    Task LogChangesAsync(string entityType, string entityName, RuleEntity? oldRule, RuleEntity newRule, string? changedBy);
    Task<List<RuleAuditLog>> GetAuditLogsAsync(string ruleName);
}

public class RuleAuditService : IRuleAuditService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RuleAuditService> _logger;

    public RuleAuditService(IServiceScopeFactory scopeFactory, ILogger<RuleAuditService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task LogAsync(string entityType, string entityName, string action, string? fieldName, string? oldValue, string? newValue, string? changedBy)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RuleDbContext>();

        var log = new RuleAuditLog
        {
            EntityType = entityType,
            EntityName = entityName,
            Action = action,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedBy = changedBy ?? "System",
            ChangedDate = DateTime.UtcNow
        };

        context.RuleAuditLogs.Add(log);
        await context.SaveChangesAsync();
        _logger.LogInformation("Audit log saved: {Action} {EntityType} {EntityName}", action, entityType, entityName);
    }

    public async Task LogChangesAsync(string entityType, string entityName, RuleEntity? oldRule, RuleEntity newRule, string? changedBy)
    {
        if (oldRule is null)
        {
            await LogAsync(entityType, entityName, "Create", null, null, JsonConvert.SerializeObject(newRule.ToRulesEngineRule()), changedBy);
            return;
        }

        var oldDef = oldRule.ToRulesEngineRule();
        var newDef = newRule.ToRulesEngineRule();

        if (oldRule.RuleName != newRule.RuleName)
        {
            await LogAsync(entityType, entityName, "Update", "RuleName", oldRule.RuleName, newRule.RuleName, changedBy);
        }

        if (oldRule.Enabled != newRule.Enabled)
        {
            await LogAsync(entityType, entityName, "Update", "Enabled", oldRule.Enabled.ToString(), newRule.Enabled.ToString(), changedBy);
        }

        if (oldRule.SampleJson != newRule.SampleJson)
        {
            await LogAsync(entityType, entityName, "Update", "SampleJson", oldRule.SampleJson, newRule.SampleJson, changedBy);
        }

        if (oldDef is not null && newDef is not null)
        {
            // Diff individual definition fields
            if (oldDef.Expression != newDef.Expression)
                await LogAsync(entityType, entityName, "Update", "Expression", oldDef.Expression, newDef.Expression, changedBy);

            if (oldDef.SuccessEvent != newDef.SuccessEvent)
                await LogAsync(entityType, entityName, "Update", "SuccessEvent", oldDef.SuccessEvent, newDef.SuccessEvent, changedBy);

            if (oldDef.ErrorMessage != newDef.ErrorMessage)
                await LogAsync(entityType, entityName, "Update", "ErrorMessage", oldDef.ErrorMessage, newDef.ErrorMessage, changedBy);

            if (oldDef.Operator != newDef.Operator)
                await LogAsync(entityType, entityName, "Update", "Operator", oldDef.Operator, newDef.Operator, changedBy);

            var oldParams = oldDef.LocalParams != null ? JsonConvert.SerializeObject(oldDef.LocalParams) : null;
            var newParams = newDef.LocalParams != null ? JsonConvert.SerializeObject(newDef.LocalParams) : null;
            if (oldParams != newParams)
                await LogAsync(entityType, entityName, "Update", "LocalParams", oldParams, newParams, changedBy);

            var oldInjects = oldDef.WorkflowsToInject != null ? JsonConvert.SerializeObject(oldDef.WorkflowsToInject) : null;
            var newInjects = newDef.WorkflowsToInject != null ? JsonConvert.SerializeObject(newDef.WorkflowsToInject) : null;
            if (oldInjects != newInjects)
                await LogAsync(entityType, entityName, "Update", "WorkflowsToInject", oldInjects, newInjects, changedBy);

            var oldActions = oldDef.Actions != null ? JsonConvert.SerializeObject(oldDef.Actions) : null;
            var newActions = newDef.Actions != null ? JsonConvert.SerializeObject(newDef.Actions) : null;
            if (oldActions != newActions)
                await LogAsync(entityType, entityName, "Update", "Actions", oldActions, newActions, changedBy);

            var oldRules = oldDef.Rules != null ? JsonConvert.SerializeObject(oldDef.Rules) : null;
            var newRules = newDef.Rules != null ? JsonConvert.SerializeObject(newDef.Rules) : null;
            if (oldRules != newRules)
                await LogAsync(entityType, entityName, "Update", "Rules", oldRules, newRules, changedBy);
        }
    }

    public async Task<List<RuleAuditLog>> GetAuditLogsAsync(string ruleName)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RuleDbContext>();
        return await context.RuleAuditLogs
            .Where(x => x.EntityName == ruleName)
            .OrderByDescending(x => x.ChangedDate)
            .ToListAsync();
    }
}
