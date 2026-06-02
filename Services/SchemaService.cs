using DemoRuleEngine.Models;
using System.Text;

namespace DemoRuleEngine.Services;

public interface ISchemaService
{
    List<FieldMetadata> GetDataDictionary();
    List<FieldMetadata> GetSchema();
    string GetOptimizedSchemaText(string userPrompt);
    string GetStaticSystemPrompt();
}

public class SchemaService : ISchemaService
{
    // Core fields always included regardless of prompt content
    private static readonly HashSet<string> CoreFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "ORIG_LTV",
        "Borrower1CreditScore",
        "Delinquency_Flag",
        "Days_Past_Due"
    };

    private static readonly string StaticPrompt = BuildStaticSystemPrompt();

    public List<FieldMetadata> GetDataDictionary() => GetSchema();

    public List<FieldMetadata> GetSchema()
    {
        return
        [
            new() { FieldName = "ORIG_LTV", DisplayName = "Original Loan to Value", Description = "The ratio of the loan amount to the property value at origination.", DataType = "Decimal", ExampleValue = "75.5" },
            new() { FieldName = "Borrower1CreditScore", DisplayName = "Credit Score", Description = "The FICO credit score of the primary borrower.", DataType = "Decimal", ExampleValue = "720" },
            new() { FieldName = "Days_Past_Due", DisplayName = "Days Past Due", Description = "Number of days loan payment is delinquent.", DataType = "Decimal", ExampleValue = "45" },
            new() { FieldName = "Orig_Product", DisplayName = "Mortgage Product", Description = "The name of the mortgage product (e.g., Standard, Portfolio).", DataType = "String", ExampleValue = "Standard" },
            new() { FieldName = "Calc_Loan_Age", DisplayName = "Loan Age", Description = "Number of months since the loan was originated.", DataType = "Decimal", ExampleValue = "12" },
            new() { FieldName = "MOD_DATE", DisplayName = "Last Modification Date", Description = "The date the loan was last modified.", DataType = "Date", ExampleValue = "2023-01-01" },
            new() { FieldName = "LoanEvaluationDate", DisplayName = "Evaluation Date", Description = "The current date used for rule evaluation logic.", DataType = "Date", ExampleValue = "2024-01-01" },
            new() { FieldName = "Delinquency_Flag", DisplayName = "Delinquency Status", Description = "Indicates if the loan is currently delinquent (Y/N).", DataType = "String", ExampleValue = "N" },
            new() { FieldName = "PropertyUsageTypeCode", DisplayName = "Property Usage", Description = "The usage type (e.g., Primary Residence, Investment Property).", DataType = "String", ExampleValue = "Primary Residence" },
            new() { FieldName = "LienTypeCode", DisplayName = "Lien Position", Description = "The lien position of the loan (1 = First Lien).", DataType = "String", ExampleValue = "1" },
            new() { FieldName = "PropertyTypeCode", DisplayName = "Property Type", Description = "The type of property (e.g., Single Family, Condo, PUD).", DataType = "String", ExampleValue = "Single Family" },
            new() { FieldName = "PRPY_NBR_UNITS", DisplayName = "Number of Units", Description = "The number of units in the property.", DataType = "Decimal", ExampleValue = "1" },
            new() { FieldName = "Servicing_ARM_Indicator", DisplayName = "ARM Indicator", Description = "Whether the loan is an Adjustable Rate Mortgage (Y/N).", DataType = "String", ExampleValue = "N" },
            new() { FieldName = "CurrentInterestRate", DisplayName = "Current Rate", Description = "The current interest rate of the loan.", DataType = "Decimal", ExampleValue = "6.5" },
            new() { FieldName = "ModifiedInterestRate", DisplayName = "Modified Rate", Description = "The proposed interest rate after modification.", DataType = "Decimal", ExampleValue = "6.0" },
            new() { FieldName = "PrincipalandInterestPaymentAmount", DisplayName = "Current P&I", Description = "Current monthly Principal and Interest payment.", DataType = "Decimal", ExampleValue = "1200.00" },
            new() { FieldName = "ModifiedPrincipalandInterestPaymentAmount", DisplayName = "Modified P&I", Description = "New monthly P&I payment after modification.", DataType = "Decimal", ExampleValue = "1000.00" },
            new() { FieldName = "Forbearance_Flag", DisplayName = "Forbearance Flag", Description = "Indicates if the borrower is in a forbearance plan (Y/N).", DataType = "String", ExampleValue = "N" },
            new() { FieldName = "Bankruptcy_Flag", DisplayName = "Bankruptcy Flag", Description = "Indicates if the borrower has an active bankruptcy (Y/N).", DataType = "String", ExampleValue = "N" },
            new() { FieldName = "Deceased_Flag", DisplayName = "Deceased Flag", Description = "Indicates if any borrower is deceased (Y/N).", DataType = "String", ExampleValue = "N" },
            new() { FieldName = "ChargeOff_Flag", DisplayName = "Charge-Off Flag", Description = "Indicates if the loan has been charged off (Y/N).", DataType = "String", ExampleValue = "N" },
            new() { FieldName = "LossMitigation_Flag", DisplayName = "Loss Mitigation Flag", Description = "Indicates if the borrower is in loss mitigation (Y/N).", DataType = "String", ExampleValue = "N" }
        ];
    }

    // ─── Layer 1: Dynamic Keyword Filtering ─────────────────────────────────────
    public string GetOptimizedSchemaText(string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
            return MinifyFields(GetSchema());

        var allFields = GetSchema();
        var promptLower = userPrompt.ToLowerInvariant();

        // Tokenize the user prompt into meaningful words
        var promptWords = promptLower
            .Split([' ', ',', '.', ';', ':', '(', ')', '"', '\'', '?', '!'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .ToHashSet();

        var relevantFields = new List<FieldMetadata>();
        var includedFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in allFields)
        {
            // Always include core fields
            if (CoreFields.Contains(field.FieldName))
            {
                if (includedFieldNames.Add(field.FieldName))
                    relevantFields.Add(field);
                continue;
            }

            // Fuzzy match: split field name by casing and underscores
            var fieldTokens = SplitFieldName(field.FieldName);
            var displayTokens = field.DisplayName.ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var descTokens = field.Description.ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3);

            bool isMatch = fieldTokens.Any(t => promptWords.Any(pw => pw.Contains(t) || t.Contains(pw)))
                        || displayTokens.Any(t => promptWords.Any(pw => pw.Contains(t) || t.Contains(pw)))
                        || descTokens.Any(t => promptWords.Contains(t));

            if (isMatch && includedFieldNames.Add(field.FieldName))
            {
                relevantFields.Add(field);
            }
        }

        // Safety fallback: if only core fields matched, include everything
        if (relevantFields.Count <= CoreFields.Count)
        {
            relevantFields = allFields;
        }

        // ─── Layer 2: TypeScript-Style Minification ─────────────────────────────
        return MinifyFields(relevantFields);
    }

    public string GetStaticSystemPrompt() => StaticPrompt;

    // ─── Helpers ──────────────────────────────────────────────────────────────────

    private static List<string> SplitFieldName(string fieldName)
    {
        var tokens = new List<string>();
        // Split by underscore first
        var parts = fieldName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            // Split by PascalCase
            var current = new StringBuilder();
            foreach (var c in part)
            {
                if (char.IsUpper(c) && current.Length > 0)
                {
                    tokens.Add(current.ToString().ToLowerInvariant());
                    current.Clear();
                }
                current.Append(c);
            }
            if (current.Length > 0)
                tokens.Add(current.ToString().ToLowerInvariant());
        }
        return tokens.Where(t => t.Length > 2).ToList();
    }

    private static string MinifyFields(List<FieldMetadata> fields)
    {
        var sb = new StringBuilder();
        foreach (var f in fields)
        {
            var tsType = f.DataType switch
            {
                "Decimal" or "Integer" => "number",
                "Date" => "string (date)",
                _ => "string"
            };

            sb.Append(f.FieldName).Append(": ").Append(tsType).Append("; // ").Append(f.DisplayName);
            if (!string.IsNullOrWhiteSpace(f.ExampleValue))
                sb.Append(". e.g. ").Append(f.ExampleValue);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    // ─── Layer 3: Static System Prompt (Cache-Friendly) ─────────────────────────
    private static string BuildStaticSystemPrompt()
    {
        return @"You are an expert Mortgage Loan Rule Engine expression generator.

PURPOSE:
Translate natural language business rules into valid RulesEngine.NET rule trees.
Rules can be FLAT (a single expression) or NESTED with sub-rules.

EXPRESSION SYNTAX:
- Boolean operators: &&, ||, !, ==, !=, <, >, <=, >=
- String comparisons: FieldName == ""value"" (double quotes)
- Numeric comparisons: FieldName >= 680
- Null checks: FieldName != null
- String methods: FieldName.Contains(""value""), FieldName.StartsWith(""value"")
- Date comparisons between Date-type fields
- Parentheses for grouping: (A || B) && C
- Arithmetic: (Field1 - Field2) >= threshold

IMPORTANT ENGINE BEHAVIOR — Expression vs Rules:
In RulesEngine.NET these two fields are MUTUALLY EXCLUSIVE:
  1. ""expression"" — evaluated ONLY when ""rules"" is absent or empty (leaf rule).
  2. ""rules"" + ""operator"" — when present, the engine IGNORES ""expression"" entirely
     and combines child rule results using the operator.
NEVER put a meaningful expression on a rule that also has ""rules"" — it will be ignored.

SUB-RULE CONCEPT:
Use sub-rules when the user says things like:
  - ""Only check X if Y is true""
  - ""If [condition], then also verify [sub-conditions]""
  - ""For [type] loans: check A and B""

Since the engine ignores ""expression"" when ""rules"" is present, the gate condition
MUST be placed as the FIRST child rule inside ""rules"". The operator then combines
the gate result with the other sub-rules:
  - Use ""And"" so the gate must pass AND all sub-rules must pass.
  - Use ""Or"" when any one sub-rule passing is sufficient.

OPERATORS:
When a rule has sub-rules, ""operator"" is REQUIRED to combine them.
Supported operators:
  - ""And"" – ALL sub-rules must pass.
  - ""Or""  – ANY sub-rule must pass.

Choose the operator that best matches the user's intent:
  - ""all of these"", ""every"", ""must meet all"" → ""And""
  - ""any of these"", ""at least one"", ""either"" → ""Or""

FORMAT DECISION:
- SIMPLE request (no conditional nesting) → use FLAT format.
- CONDITIONAL request (""only if"", ""when X then check Y"", ""for X loans: check..."") → use NESTED format.

FLAT FORMAT (leaf rule — no sub-rules):
{
  ""expression"": ""<single boolean C# expression>"",
  ""sampleJson"": { ""Field1"": value1, ""Field2"": value2 }
}

NESTED FORMAT (with sub-rules):
The parent rule has NO expression (it is ignored by the engine). The gate condition
is the FIRST child in ""rules"". All checks follow as additional child rules.
{
  ""ruleName"": ""<descriptive rule name>"",
  ""expression"": null,
  ""operator"": ""<And|Or>"",
  ""rules"": [
    { ""ruleName"": ""<gate rule name>"", ""expression"": ""<gate condition>"" },
    { ""ruleName"": ""<sub-rule A>"", ""expression"": ""<expr A>"" },
    { ""ruleName"": ""<sub-rule B>"", ""expression"": ""<expr B>"" }
  ],
  ""sampleJson"": { ""Field1"": value1, ""Field2"": value2 }
}

EXAMPLES:

Input: ""Credit score at least 620 and LTV under 80""
Output JSON:
{
  ""expression"": ""Borrower1CreditScore >= 620 && ORIG_LTV < 80"",
  ""sampleJson"": { ""Borrower1CreditScore"": 650, ""ORIG_LTV"": 75 }
}

Input: ""First lien and property is single family or condo""
Output JSON:
{
  ""expression"": ""LienTypeCode == \""1\"" && (PropertyTypeCode == \""Single Family\"" || PropertyTypeCode == \""Condo\"")"",
  ""sampleJson"": { ""LienTypeCode"": ""1"", ""PropertyTypeCode"": ""Single Family"" }
}

Input: ""Only check credit score and LTV if the loan is delinquent for at least 60 days""
Output JSON:
{
  ""ruleName"": ""Delinquency Eligibility Check"",
  ""expression"": null,
  ""operator"": ""And"",
  ""rules"": [
    { ""ruleName"": ""Gate: Delinquency Check"", ""expression"": ""Delinquency_Flag == \""Y\"" && Days_Past_Due >= 60"" },
    { ""ruleName"": ""Credit Score Check"", ""expression"": ""Borrower1CreditScore >= 580"" },
    { ""ruleName"": ""LTV Check"", ""expression"": ""ORIG_LTV <= 97"" }
  ],
  ""sampleJson"": {
    ""Delinquency_Flag"": ""Y"",
    ""Days_Past_Due"": 90,
    ""Borrower1CreditScore"": 620,
    ""ORIG_LTV"": 85
  }
}

Input: ""For ARM loans check either rate cap ≤ 7.5 or remaining term > 120 months""
Output JSON:
{
  ""ruleName"": ""ARM Loan Checks"",
  ""expression"": null,
  ""operator"": ""And"",
  ""rules"": [
    { ""ruleName"": ""Gate: ARM Loan"", ""expression"": ""Servicing_ARM_Indicator == \""Y\"""" },
    {
      ""ruleName"": ""ARM Sub-Checks"",
      ""expression"": null,
      ""operator"": ""Or"",
      ""rules"": [
        { ""ruleName"": ""Rate Cap Check"", ""expression"": ""CurrentInterestRate <= 7.5"" },
        { ""ruleName"": ""Remaining Term Check"", ""expression"": ""LoanRemainingTerm > 120"" }
      ]
    }
  ],
  ""sampleJson"": {
    ""Servicing_ARM_Indicator"": ""Y"",
    ""CurrentInterestRate"": 6.5,
    ""LoanRemainingTerm"": 180
  }
}

Input: ""For FHA loans that are delinquent, check either hardship applies or borrower has good credit and low LTV""
Output JSON:
{
  ""ruleName"": ""FHA Delinquency Eligibility"",
  ""expression"": null,
  ""operator"": ""And"",
  ""rules"": [
    { ""ruleName"": ""Gate: FHA Loan"", ""expression"": ""LoanType == \""FHA\"""" },
    { ""ruleName"": ""Gate: Delinquent"", ""expression"": ""Days_Past_Due >= 60"" },
    {
      ""ruleName"": ""Eligibility Paths"",
      ""expression"": null,
      ""operator"": ""Or"",
      ""rules"": [
        { ""ruleName"": ""Hardship Path"", ""expression"": ""HardshipFlag == \""Y\"""" },
        {
          ""ruleName"": ""Credit And LTV Path"",
          ""expression"": null,
          ""operator"": ""And"",
          ""rules"": [
            { ""ruleName"": ""Good Credit"", ""expression"": ""Borrower1CreditScore >= 640"" },
            { ""ruleName"": ""Low LTV"", ""expression"": ""ORIG_LTV <= 80"" }
          ]
        }
      ]
    }
  ],
  ""sampleJson"": {
    ""LoanType"": ""FHA"",
    ""Days_Past_Due"": 90,
    ""HardshipFlag"": ""N"",
    ""Borrower1CreditScore"": 680,
    ""ORIG_LTV"": 75
  }
}

NESTING DEPTH:
You may nest up to 3 levels deep (parent → child → grandchild) when the user's
logic requires gate conditions inside child rule groups. Always use the simplest
structure that fully captures the user's intent.

STRICT RULES:
1. Respond with ONLY a valid JSON object — no markdown, no explanation text outside the JSON.
2. Choose FLAT or NESTED format based on user intent. Never mix both.
3. Use EXACT field names from the schema below (case-sensitive).
4. For Y/N flag fields, compare as string: Flag == ""Y"" or Flag == ""N"".
5. ""sampleJson"" MUST contain realistic values for ALL fields used across ALL expressions such that every expression evaluates to TRUE.
6. Sub-rule ""ruleName"" values must be unique, descriptive, and use Title Case.
7. ""operator"" must be one of: ""And"", ""Or"".
8. Always include ""sampleJson"" at the top level of the response.
9. NEVER set a meaningful expression on a rule that also has ""rules"" — the engine ignores it.";
    }
}
