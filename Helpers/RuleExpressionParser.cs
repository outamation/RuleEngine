using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RulesEngine.Models;

namespace DemoRuleEngine.Helpers;

public static class RuleExpressionParser
{
    private static readonly HashSet<string> IgnoredTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "true", "false", "null", "and", "or", "&&", "||", "new", "typeof", "Convert", "Math",
        "RuleHelper", "In", "ExactMatch", "FuzzyMatch", "num", "Sum", 
        "MonthsDifference", "DaysDifference", "HasValue", "HasAnyTermMismatch"
    };

    /// <summary>
    /// Recursively extracts all field names from a rule and its sub-rules.
    /// </summary>
    public static HashSet<string> ExtractFieldsFromRule(Rule rule)
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (rule == null) return fields;

        // Process current rule expression
        if (!string.IsNullOrWhiteSpace(rule.Expression))
        {
            var ruleFields = ExtractFieldsFromExpression(rule.Expression);
            fields.UnionWith(ruleFields);
        }

        // Process local params
        if (rule.LocalParams != null)
        {
            foreach (var lp in rule.LocalParams)
            {
                if (!string.IsNullOrWhiteSpace(lp.Expression))
                {
                    fields.UnionWith(ExtractFieldsFromExpression(lp.Expression));
                }
            }
        }

        // Process sub-rules recursively
        if (rule.Rules != null)
        {
            foreach (var subRule in rule.Rules)
            {
                fields.UnionWith(ExtractFieldsFromRule(subRule));
            }
        }

        return fields;
    }

    private static HashSet<string> ExtractFieldsFromExpression(string expression)
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(expression)) return fields;

        // 1. Sanitize hyphenated fields (e.g. DLQ1-Loan_type -> DLQ1_Loan_type)
        var sanitized = Regex.Replace(expression, @"\b([a-zA-Z_][a-zA-Z0-9_]*)-([a-zA-Z_][a-zA-Z0-9_]*)\b", "$1_$2");

        // 2. Remove string literals to avoid capturing comparison values (e.g. "CONV", "NY")
        var cleanExpr = Regex.Replace(sanitized, @"""[^""]*""", "");

        // 3. Match all valid C# identifiers
        var matches = Regex.Matches(cleanExpr, @"\b[a-zA-Z_][a-zA-Z0-9_]*\b");

        foreach (Match match in matches)
        {
            var token = match.Value;
            if (!IgnoredTokens.Contains(token))
            {
                fields.Add(token);
            }
        }

        return fields;
    }

    /// <summary>
    /// Merges the extracted fields into the existing Sample JSON string, preserving current values.
    /// </summary>
    public static string MergeFieldsIntoSampleJson(string? currentJson, HashSet<string> fieldsUsed)
    {
        JObject jsonObject;

        try
        {
            jsonObject = !string.IsNullOrWhiteSpace(currentJson) 
                ? JObject.Parse(currentJson) 
                : new JObject();
        }
        catch
        {
            // Fallback if the current sample JSON is malformed
            jsonObject = new JObject();
        }

        // Add any missing field as null
        foreach (var field in fieldsUsed)
        {
            if (!jsonObject.ContainsKey(field))
            {
                jsonObject[field] = null;
            }
        }

        return jsonObject.ToString(Formatting.Indented);
    }
}
