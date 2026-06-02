using DemoRuleEngine.Models;
using DemoRuleEngine.Helpers;
using RulesEngine.Models;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text.Json;
using DemoRuleEngine.Data;
using Microsoft.EntityFrameworkCore;

namespace DemoRuleEngine.Services;

public class EligibilityService : IEligibilityService
{
    private readonly IRuleManagerService _ruleManager;
    private readonly ILogger<EligibilityService> _logger;
    private readonly RuleDbContext _db;

    public EligibilityService(IRuleManagerService ruleManager, ILogger<EligibilityService> logger, RuleDbContext db)
    {
        _ruleManager = ruleManager;
        _logger = logger;
        _db = db;
    }

    public async Task<EligibilityResult> EvaluateAsync(string workflowName, object inputData)
    {
        var response = new EligibilityResult();

        try
        {
            var applicationData = NormalizeInputData(inputData);

            // Flatten { value, confidence } objects into plain values for rule evaluation
            var flattenedData = FlattenInputData(applicationData);

            var engine = _ruleManager.GetEngine();
            var ruleParams = new RuleParameter("input", flattenedData);
            var results = await engine.ExecuteAllRulesAsync(workflowName, ruleParams);

            // Retrieve all rules for this workflow from DB to get their FieldsUsed properties
            var workflowEntity = await _db.Workflows
                .Include(w => w.Rules)
                .FirstOrDefaultAsync(w => w.WorkflowName == workflowName);

            var rulesDict = workflowEntity?.Rules?
                .ToDictionary(r => r.RuleName, r => r, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, RuleEntity>(StringComparer.OrdinalIgnoreCase);

            response.TotalRulesEvaluated = results.Count;
            response.RulesPassed = results.Count(r => r.IsSuccess);
            response.RulesFailed = results.Count(r => !r.IsSuccess);
            response.IsEligible = results.All(r => r.IsSuccess);

            foreach (var r in results)
            {
                rulesDict.TryGetValue(r.Rule.RuleName, out var dbRule);

                var outcome = new RuleOutcome
                {
                    RuleName = r.Rule.RuleName,
                    IsSuccess = r.IsSuccess,
                    SuccessMessage = r.IsSuccess 
                        ? (string.IsNullOrEmpty(r.Rule.SuccessEvent) ? "No-No Exception" : r.Rule.SuccessEvent)
                        : null,
                    ErrorMessage = !r.IsSuccess
                        ? (string.IsNullOrEmpty(r.Rule.ErrorMessage) ? "Exception" : FormatDynamicPlaceholders(r.Rule.ErrorMessage, flattenedData))
                        : null,
                    ConfidenceScore = CalculateLowestConfidence(r, inputData, dbRule)
                };

                // Get individual field failures for the comment
                if (!r.IsSuccess && r.ChildResults != null && r.ChildResults.Any())
                {
                    var failedFields = r.ChildResults
                        .Where(c => !c.IsSuccess)
                        .Select(c => {
                            string baseMsg = "";
                            if (!string.IsNullOrEmpty(c.Rule.ErrorMessage))
                            {
                                baseMsg = c.Rule.ErrorMessage;
                            }
                            else if (c.Rule.Properties != null && c.Rule.Properties.TryGetValue("FailureMessage", out var fm) && fm != null)
                            {
                                baseMsg = fm.ToString() ?? "";
                            }
                            
                            if (string.IsNullOrEmpty(baseMsg))
                            {
                                baseMsg = $"{c.Rule.RuleName} failed";
                            }
                            return FormatDynamicPlaceholders(baseMsg, flattenedData);
                        })
                        .Where(msg => !string.IsNullOrEmpty(msg))
                        .ToList();

                    outcome.Comment = failedFields.Any() 
                        ? string.Join("; ", failedFields) 
                        : null;
                }

                // Recursively look for any real C# exception occurred within this rule or its sub-rules
                var actualException = GetActualExceptionMessage(r);
                if (!string.IsNullOrEmpty(actualException))
                {
                    outcome.IsSuccess = false;
                    outcome.ExceptionMessage = FormatErrorMessage(actualException);
                }

                response.Details.Add(outcome);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during rule evaluation for workflow {Workflow}", workflowName);
            response.IsEligible = false;
            response.Details.Add(new RuleOutcome
            {
                RuleName = "System.EvaluationError",
                IsSuccess = false,
                ErrorMessage = "A critical error occurred during evaluation.",
                ExceptionMessage = ex.Message
            });
        }

        return response;
    }

    public async Task<AdhocEvaluateResult> EvaluateAdhocAsync(AdhocEvaluateRequest request)
    {
        var result = new AdhocEvaluateResult();

        try
        {
            var inputObject = NormalizeInputData(request.SampleJson);
            var flattenedData = FlattenInputData(inputObject);

            // Sanitize hyphenated fields in the adhoc expression
            var sanitizedExpression = string.IsNullOrWhiteSpace(request.Expression)
                ? ""
                : Regex.Replace(request.Expression, @"\b([a-zA-Z_][a-zA-Z0-9_]*)-([a-zA-Z_][a-zA-Z0-9_]*)\b", "$1_$2");

            // Construct a temporary rule
            var rule = new Rule
            {
                RuleName = "AdhocRule",
                Expression = sanitizedExpression,
                RuleExpressionType = RuleExpressionType.LambdaExpression
            };

            // Wrap rule inside a temporary workflow
            var workflow = new Workflow
            {
                WorkflowName = "AdhocWorkflow",
                Rules = new List<Rule> { rule }
            };

            var reSettings = new ReSettings
            {
                CustomTypes = new[] { typeof(RuleHelper) }
            };

            var engine = new RulesEngine.RulesEngine(new[] { workflow }, reSettings);
            var ruleParams = new RuleParameter("input", flattenedData);
            var results = await engine.ExecuteAllRulesAsync("AdhocWorkflow", ruleParams);

            var ruleResult = results.FirstOrDefault();
            if (ruleResult is not null)
            {
                result.IsSuccess = ruleResult.IsSuccess;
                
                var actualException = GetActualExceptionMessage(ruleResult);
                result.ExceptionMessage = !string.IsNullOrEmpty(actualException)
                    ? FormatErrorMessage(actualException)
                    : null;
            }
            else
            {
                result.IsSuccess = false;
                result.ExceptionMessage = "No evaluation results were returned.";
            }
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ExceptionMessage = ex.Message;
        }

        return result;
    }

    private object NormalizeInputData(object inputData)
    {
        string? json = null;

        if (inputData is string str)
        {
            json = str;
        }
        else if (inputData is System.Text.Json.JsonElement jsonElement)
        {
            json = jsonElement.GetRawText();
        }
        else if (inputData is Newtonsoft.Json.Linq.JToken token)
        {
            json = token.ToString();
        }
        else if (inputData is not null)
        {
            json = JsonConvert.SerializeObject(inputData);
        }

        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                return JsonConvert.DeserializeObject<ExpandoObject>(json)!;
            }
            catch
            {
                // Fallback if parsing to ExpandoObject fails
            }
        }

        return inputData ?? new ExpandoObject();
    }

    private object FlattenInputData(object normalizedData)
    {
        if (normalizedData is not IDictionary<string, object> dict)
            return normalizedData;

        var flattened = new ExpandoObject() as IDictionary<string, object>;

        foreach (var kvp in dict)
        {
            if (kvp.Value is IDictionary<string, object> nested && nested.ContainsKey("value"))
            {
                flattened[kvp.Key] = nested["value"];
            }
            else
            {
                flattened[kvp.Key] = kvp.Value;
            }
        }

        // Duplicate keys with hyphens replaced by underscores to support dynamic LINQ compatibility
        var keys = flattened.Keys.ToList();
        foreach (var key in keys)
        {
            if (key.Contains('-'))
            {
                var newKey = key.Replace('-', '_');
                flattened[newKey] = flattened[key];
            }
        }

        return flattened;
    }

    private string FormatErrorMessage(string technicalMessage)
    {
        if (technicalMessage.Contains("does not contain a definition for"))
        {
            var match = Regex.Match(technicalMessage, @"'([^']+)'");
            var fieldName = match.Success ? match.Groups[1].Value : "a required field";
            return $"Evaluation Error: The field '{fieldName}' was not provided in the input data.";
        }
        if (technicalMessage.Contains("No property or field") && technicalMessage.Contains("exists in type"))
        {
            var match = Regex.Match(technicalMessage, @"No property or field '([^']+)'");
            var fieldName = match.Success ? match.Groups[1].Value : "a required field";
            return $"Evaluation Error: The field '{fieldName}' was not provided in the input data.";
        }
        
        return technicalMessage;
    }

    /// <summary>
    /// Recursively scans the RuleResultTree to find any real C# dynamic evaluation exception message.
    /// In Microsoft RulesEngine, child/sub-rule exceptions are stored in child nodes while the parent
    /// node's ExceptionMessage might only contain a generic "Exception" string.
    /// </summary>
    private string? GetActualExceptionMessage(RuleResultTree result)
    {
        if (result == null) return null;

        if (!string.IsNullOrEmpty(result.ExceptionMessage))
        {
            var ignoredMessages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Exception",
                "No-No Exception",
                "Yes-No Exception",
                "No-Exception"
            };

            // Check if the exception message matches any of the ignored standard messages
            bool isIgnoredMessage = ignoredMessages.Contains(result.ExceptionMessage.Trim());

            // Or if it matches the rule's configured ErrorMessage (trimmed, case-insensitive)
            bool isNormalFailureMessage = result.Rule != null && 
                result.ExceptionMessage.Trim().Equals(result.Rule.ErrorMessage?.Trim(), StringComparison.OrdinalIgnoreCase);

            if (!isIgnoredMessage && !isNormalFailureMessage)
            {
                return result.ExceptionMessage;
            }
        }

        // Recursively inspect children rules (sub-rules) to find any nested exception details
        if (result.ChildResults != null)
        {
            foreach (var child in result.ChildResults)
            {
                var msg = GetActualExceptionMessage(child);
                if (!string.IsNullOrEmpty(msg))
                {
                    return msg;
                }
            }
        }

        return null;
    }

    private List<string> GetExpressionsRecursive(RuleResultTree result)
    {
        var expressions = new List<string>();
        if (result == null) return expressions;

        if (result.Rule != null && !string.IsNullOrWhiteSpace(result.Rule.Expression))
        {
            expressions.Add(result.Rule.Expression);
        }

        if (result.ChildResults != null)
        {
            foreach (var child in result.ChildResults)
            {
                expressions.AddRange(GetExpressionsRecursive(child));
            }
        }

        return expressions;
    }

    private double? CalculateLowestConfidence(RuleResultTree result, object inputData, RuleEntity? dbRule)
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Try to read pre-parsed fields from the Rule Entity from database first
        if (dbRule?.Definition?.Properties != null &&
            dbRule.Definition.Properties.TryGetValue("FieldsUsed", out var dbFieldsObj))
        {
            if (dbFieldsObj is IEnumerable<object> fieldsList)
            {
                foreach (var f in fieldsList)
                {
                    if (f is string s)
                    {
                        fields.Add(s);
                    }
                }
            }
            else if (dbFieldsObj is Newtonsoft.Json.Linq.JArray jArray)
            {
                foreach (var token in jArray)
                {
                    var val = token.ToString();
                    if (!string.IsNullOrEmpty(val))
                    {
                        fields.Add(val);
                    }
                }
            }
        }
        // 2. Fallback to in-memory result.Rule.Properties (if the DB entity wasn't found or passed)
        else if (result.Rule?.Properties != null &&
                 result.Rule.Properties.TryGetValue("FieldsUsed", out var fieldsObj))
        {
            if (fieldsObj is IEnumerable<object> fieldsList)
            {
                foreach (var f in fieldsList)
                {
                    if (f is string s)
                    {
                        fields.Add(s);
                    }
                }
            }
            else if (fieldsObj is Newtonsoft.Json.Linq.JArray jArray)
            {
                foreach (var token in jArray)
                {
                    var val = token.ToString();
                    if (!string.IsNullOrEmpty(val))
                    {
                        fields.Add(val);
                    }
                }
            }
        }

        // 2. Fall back to on-the-fly regex parsing if Properties metadata is not present (legacy rules)
        if (!fields.Any())
        {
            var expressions = GetExpressionsRecursive(result);
            if (!expressions.Any()) return null;

            var wordRegex = new Regex(@"\b[a-zA-Z_]\w*\b", RegexOptions.Compiled);
            var reservedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "null", "true", "false", "new", "DateTime", "TimeSpan", "Math", "Convert", 
                "string", "int", "decimal", "double", "bool", "guid", "RuleHelper", "input", "input1"
            };

            foreach (var expr in expressions)
            {
                var matches = wordRegex.Matches(expr);
                foreach (Match match in matches)
                {
                    var word = match.Value;

                    // Skip reserved keywords
                    if (reservedKeywords.Contains(word)) continue;

                    // Check if followed by '(' (method call)
                    int nextCharIdx = match.Index + match.Length;
                    while (nextCharIdx < expr.Length && char.IsWhiteSpace(expr[nextCharIdx]))
                    {
                        nextCharIdx++;
                    }
                    if (nextCharIdx < expr.Length && expr[nextCharIdx] == '(')
                    {
                        continue; // Method call, skip
                    }

                    // Check if preceded by '.' (nested property access, e.g. .value)
                    int prevCharIdx = match.Index - 1;
                    while (prevCharIdx >= 0 && char.IsWhiteSpace(expr[prevCharIdx]))
                    {
                        prevCharIdx--;
                    }

                    if (prevCharIdx >= 0 && expr[prevCharIdx] == '.')
                    {
                        // Check if preceded by 'input' or 'input\d+'
                        int wordStartIdx = prevCharIdx - 1;
                        while (wordStartIdx >= 0 && char.IsWhiteSpace(expr[wordStartIdx]))
                        {
                            wordStartIdx--;
                        }
                        if (wordStartIdx >= 0 && (char.IsLetterOrDigit(expr[wordStartIdx]) || expr[wordStartIdx] == '_'))
                        {
                            int wordEndIdx = wordStartIdx;
                            while (wordStartIdx >= 0 && (char.IsLetterOrDigit(expr[wordStartIdx]) || expr[wordStartIdx] == '_'))
                            {
                                wordStartIdx--;
                            }
                            string prevWord = expr.Substring(wordStartIdx + 1, wordEndIdx - wordStartIdx);
                            if (!string.Equals(prevWord, "input", StringComparison.OrdinalIgnoreCase) && 
                                !Regex.IsMatch(prevWord, @"^input\d+$", RegexOptions.IgnoreCase))
                            {
                                continue; // Sub-property, skip
                            }
                        }
                        else
                        {
                            continue; // Sub-property, skip
                        }
                    }

                    fields.Add(word);
                }
            }
        }

        if (!fields.Any()) return null;

        double? minConfidence = null;
        string? jsonString = null;

        if (inputData is string str)
        {
            jsonString = str;
        }
        else if (inputData is System.Text.Json.JsonElement element)
        {
            jsonString = element.GetRawText();
        }
        else if (inputData is Newtonsoft.Json.Linq.JToken token)
        {
            jsonString = token.ToString();
        }
        else if (inputData is not null)
        {
            jsonString = JsonConvert.SerializeObject(inputData);
        }

        if (string.IsNullOrWhiteSpace(jsonString)) return null;

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            foreach (var fieldName in fields)
            {
                if (root.TryGetProperty(fieldName, out var fieldObj))
                {
                    // Field is an object with a "confidence" property: { "value": "...", "confidence": 0.95 }
                    if (fieldObj.ValueKind == JsonValueKind.Object && fieldObj.TryGetProperty("confidence", out var confidenceProp))
                    {
                        if (confidenceProp.TryGetDouble(out var confVal))
                        {
                            if (minConfidence == null || confVal < minConfidence.Value)
                            {
                                minConfidence = confVal;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Fail-safe: if JSON parsing fails, return null
        }

        return minConfidence;
    }

    private static string FormatDynamicPlaceholders(string template, object inputData)
    {
        if (string.IsNullOrEmpty(template)) return template;

        // Match placeholders like {input.FieldName} — root-level fields only
        var regex = new Regex(@"\{input\.([a-zA-Z0-9_]+)\}", RegexOptions.Compiled);
        
        return regex.Replace(template, match =>
        {
            var fieldName = match.Groups[1].Value;
            var val = GetFieldValue(inputData, fieldName);
            return val?.ToString() ?? "null";
        });
    }

    private static object? GetFieldValue(object? obj, string fieldName)
    {
        if (obj == null || string.IsNullOrEmpty(fieldName)) return null;

        // Primary path: ExpandoObject / dictionary (from NormalizeInputData)
        if (obj is IDictionary<string, object> dict)
        {
            if (dict.TryGetValue(fieldName, out var val))
                return val;

            // Fallback: case-insensitive key match
            var key = dict.Keys.FirstOrDefault(k => k.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
            return key != null ? dict[key] : null;
        }

        // Fallback: reflection for strongly-typed objects
        var type = obj.GetType();
        var prop = type.GetProperty(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (prop != null) return prop.GetValue(obj);

        var field = type.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        return field?.GetValue(obj);
    }
}
