using DemoRuleEngine.Models;
using RulesEngine.Models;
using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DemoRuleEngine.Services;

public static class RuleExpressionBuilder
{
    public static Rule Build(RuleDefinitionDto dto)
    {
        if (dto is null)
            throw new ArgumentNullException(nameof(dto));

        var rule = new Rule
        {
            RuleName = dto.RuleName,
            Expression = dto.Expression,
            SuccessEvent = dto.SuccessMessage ?? dto.Description,
            ErrorMessage = dto.ErrorMessage ?? dto.FailureMessage,
            Enabled = dto.Enabled,
            Operator = dto.Operator,
            WorkflowsToInject = dto.WorkflowsToInject,
            Actions = dto.Actions,
            RuleExpressionType = RuleExpressionType.LambdaExpression
        };

        if (dto.LocalParams is not null && dto.LocalParams.Count > 0)
        {
            rule.LocalParams = dto.LocalParams.Select(lp => new ScopedParam
            {
                Name = lp.Name,
                Expression = lp.Expression
            }).ToList();
        }

        if (dto.Rules is not null && dto.Rules.Count > 0)
        {
            rule.Rules = dto.Rules.Select(Build).ToList();
        }

        // Apply rules engine operators logic defaults
        bool hasChildren = rule.Rules is not null && rule.Rules.Any();
        bool hasInjections = rule.WorkflowsToInject is not null && rule.WorkflowsToInject.Any();
        
        if (!hasChildren && !hasInjections)
        {
            rule.Operator = null; 
        }

        if (string.IsNullOrWhiteSpace(rule.Expression) && !hasChildren && !hasInjections)
        {
            rule.Expression = "true";
        }
        else if (hasChildren || hasInjections)
        {
            if (string.IsNullOrWhiteSpace(rule.Expression))
                rule.Expression = "true";
        }

        return rule;
    }
}
