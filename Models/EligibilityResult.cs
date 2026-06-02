namespace DemoRuleEngine.Models;

/// <summary>
/// Aggregated result of evaluating all eligibility rules against a loan application.
/// </summary>
public class EligibilityResult
{
    /// <summary>True if all rules passed (loan is eligible)</summary>
    public bool IsEligible { get; set; }

    /// <summary>Total number of rules that were evaluated</summary>
    public int TotalRulesEvaluated { get; set; }

    /// <summary>Number of rules that passed</summary>
    public int RulesPassed { get; set; }

    /// <summary>Number of rules that failed</summary>
    public int RulesFailed { get; set; }

    /// <summary>Number of rules that were skipped (disabled or parent condition not met)</summary>
    public int RulesSkipped { get; set; }

    /// <summary>Timestamp of when the evaluation was performed</summary>
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Per-rule breakdown of pass/fail results</summary>
    public List<RuleOutcome> Details { get; set; } = new();
}
