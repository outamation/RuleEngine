namespace DemoRuleEngine.Models;

/// <summary>
/// Represents the evaluation result of a single rule.
/// </summary>
public class RuleOutcome
{
    /// <summary>Name of the rule that was evaluated</summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>Whether the rule passed (true = eligible for this criterion)</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Human-readable success message (populated only if IsSuccess is true)</summary>
    public string? SuccessMessage { get; set; }

    /// <summary>Human-readable error message (populated only if IsSuccess is false)</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Specific mismatched field details for failure outcomes derived from child rules</summary>
    public string? Comment { get; set; }

    /// <summary>Actual exception message if the rule threw a runtime/parsing error during evaluation</summary>
    public string? ExceptionMessage { get; set; }

    /// <summary>The lowest confidence score among all fields involved in evaluating this rule</summary>
    public double? ConfidenceScore { get; set; }
}
