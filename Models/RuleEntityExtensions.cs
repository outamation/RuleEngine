using RulesEngine.Models;

namespace DemoRuleEngine.Models;

public static class RuleEntityExtensions
{
    public static Rule ToRulesEngineRule(this RuleEntity entity)
    {
        var rule = entity.Definition ?? new Rule();
        rule.RuleName = entity.RuleName;
        rule.Enabled = entity.Enabled;
        return rule;
    }

    public static void UpdateFromRulesEngineRule(this RuleEntity entity, Rule rule)
    {
        entity.RuleName = rule.RuleName;
        entity.Enabled = rule.Enabled;
        entity.Definition = rule;
    }
}
