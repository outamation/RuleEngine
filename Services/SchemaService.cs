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
        "Days_Past_Due",
        "investor_type",
        "property_state",
        "modified_UPB",
        "modified_interest_rate"
    };

    private static readonly string StaticPrompt = BuildStaticSystemPrompt();

    public List<FieldMetadata> GetDataDictionary() => GetSchema();

    public List<FieldMetadata> GetSchema()
    {
        return
        [
            // new() { FieldName = "ORIG_LTV", DisplayName = "Original Loan to Value", Description = "The ratio of the loan amount to the property value at origination.", DataType = "Decimal", ExampleValue = "75.5" },
            // new() { FieldName = "Borrower1CreditScore", DisplayName = "Credit Score", Description = "The FICO credit score of the primary borrower.", DataType = "Decimal", ExampleValue = "720" },
            // new() { FieldName = "Days_Past_Due", DisplayName = "Days Past Due", Description = "Number of days loan payment is delinquent.", DataType = "Decimal", ExampleValue = "45" },
            // new() { FieldName = "Orig_Product", DisplayName = "Mortgage Product", Description = "The name of the mortgage product (e.g., Standard, Portfolio).", DataType = "String", ExampleValue = "Standard" },
            // new() { FieldName = "Calc_Loan_Age", DisplayName = "Loan Age", Description = "Number of months since the loan was originated.", DataType = "Decimal", ExampleValue = "12" },
            // new() { FieldName = "MOD_DATE", DisplayName = "Last Modification Date", Description = "The date the loan was last modified.", DataType = "Date", ExampleValue = "2023-01-01" },
            // new() { FieldName = "LoanEvaluationDate", DisplayName = "Evaluation Date", Description = "The current date used for rule evaluation logic.", DataType = "Date", ExampleValue = "2024-01-01" },
            // new() { FieldName = "Delinquency_Flag", DisplayName = "Delinquency Status", Description = "Indicates if the loan is currently delinquent (Y/N).", DataType = "String", ExampleValue = "N" },
            // new() { FieldName = "PropertyUsageTypeCode", DisplayName = "Property Usage", Description = "The usage type (e.g., Primary Residence, Investment Property).", DataType = "String", ExampleValue = "Primary Residence" },
            // new() { FieldName = "LienTypeCode", DisplayName = "Lien Position", Description = "The lien position of the loan (1 = First Lien).", DataType = "String", ExampleValue = "1" },
            // new() { FieldName = "PropertyTypeCode", DisplayName = "Property Type", Description = "The type of property (e.g., Single Family, Condo, PUD).", DataType = "String", ExampleValue = "Single Family" },
            // new() { FieldName = "PRPY_NBR_UNITS", DisplayName = "Number of Units", Description = "The number of units in the property.", DataType = "Decimal", ExampleValue = "1" },
            // new() { FieldName = "Servicing_ARM_Indicator", DisplayName = "ARM Indicator", Description = "Whether the loan is an Adjustable Rate Mortgage (Y/N).", DataType = "String", ExampleValue = "N" },
            // new() { FieldName = "CurrentInterestRate", DisplayName = "Current Rate", Description = "The current interest rate of the loan.", DataType = "Decimal", ExampleValue = "6.5" },
            // new() { FieldName = "ModifiedInterestRate", DisplayName = "Modified Rate", Description = "The proposed interest rate after modification.", DataType = "Decimal", ExampleValue = "6.0" },
            // new() { FieldName = "PrincipalandInterestPaymentAmount", DisplayName = "Current P&I", Description = "Current monthly Principal and Interest payment.", DataType = "Decimal", ExampleValue = "1200.00" },
            // new() { FieldName = "ModifiedPrincipalandInterestPaymentAmount", DisplayName = "Modified P&I", Description = "New monthly P&I payment after modification.", DataType = "Decimal", ExampleValue = "1000.00" },
            // new() { FieldName = "Forbearance_Flag", DisplayName = "Forbearance Flag", Description = "Indicates if the borrower is in a forbearance plan (Y/N).", DataType = "String", ExampleValue = "N" },
            // new() { FieldName = "Bankruptcy_Flag", DisplayName = "Bankruptcy Flag", Description = "Indicates if the borrower has an active bankruptcy (Y/N).", DataType = "String", ExampleValue = "N" },
            // new() { FieldName = "Deceased_Flag", DisplayName = "Deceased Flag", Description = "Indicates if any borrower is deceased (Y/N).", DataType = "String", ExampleValue = "N" },
            // new() { FieldName = "ChargeOff_Flag", DisplayName = "Charge-Off Flag", Description = "Indicates if the loan has been charged off (Y/N).", DataType = "String", ExampleValue = "N" },
            // new() { FieldName = "LossMitigation_Flag", DisplayName = "Loss Mitigation Flag", Description = "Indicates if the borrower is in loss mitigation (Y/N).", DataType = "String", ExampleValue = "N" },
            new() { FieldName = "DLQ1_Loan_type", DisplayName = "DLQ1 Loan Type", Description = "Loan type for DLQ1 (e.g. Conventional, FHA, VA).", DataType = "String", ExampleValue = "Conventional" },
            new() { FieldName = "DLQ1_Investor_Name", DisplayName = "DLQ1 Investor Name", Description = "Investor name for DLQ1 (e.g. FNMA, FHLMC).", DataType = "String", ExampleValue = "FNMA" },
            new() { FieldName = "DLQ1_Property_State", DisplayName = "DLQ1 Property State", Description = "Property state code (2-letter format).", DataType = "String", ExampleValue = "CA" },
            new() { FieldName = "DLQ1_Property_State_CoverLetter", DisplayName = "DLQ1 Property State (Full Name)", Description = "Full name of the property state used in cover letter.", DataType = "String", ExampleValue = "California" },
            new() { FieldName = "CapitalizedUPB_SummaryReport", DisplayName = "Capitalized UPB Summary Report", Description = "Capitalized Unpaid Principal Balance in summary report.", DataType = "Decimal", ExampleValue = "250000.00" },
            new() { FieldName = "UnpaidPrincipalBalance_Mod", DisplayName = "Unpaid Principal Balance (Mod)", Description = "Unpaid Principal Balance for modification calculation.", DataType = "Decimal", ExampleValue = "245000.00" },
            new() { FieldName = "UnpaidPrincipalBalance_CoverLetter", DisplayName = "Unpaid Principal Balance (Cover Letter)", Description = "Unpaid Principal Balance used in cover letter.", DataType = "Decimal", ExampleValue = "245000.00" },
            new() { FieldName = "MaturityDate_Mod_Mod", DisplayName = "Maturity Date (Mod Mod)", Description = "Maturity date after modification.", DataType = "String", ExampleValue = "2053-06-01" },
            new() { FieldName = "Modified_Maturity_Date_CoverLetter", DisplayName = "Modified Maturity Date (Cover Letter)", Description = "Modified maturity date for cover letter.", DataType = "String", ExampleValue = "2053-06-01" },
            new() { FieldName = "MaturityDate_SummaryReport", DisplayName = "Maturity Date (Summary Report)", Description = "Maturity date for summary report.", DataType = "String", ExampleValue = "2053-06-01" },
            new() { FieldName = "OriginalMtg_MaturityDate", DisplayName = "Original Mortgage Maturity Date", Description = "Original mortgage maturity date.", DataType = "String", ExampleValue = "2053-06-01" },
            new() { FieldName = "investor_type", DisplayName = "Investor Type", Description = "The investor type (e.g. FHA, VA, Conv).", DataType = "String", ExampleValue = "FHA" },
            new() { FieldName = "property_state", DisplayName = "Property State", Description = "Property state abbreviation (e.g. TX, CA).", DataType = "String", ExampleValue = "TX" },
            new() { FieldName = "modified_UPB", DisplayName = "Modified UPB", Description = "Modified Unpaid Principal Balance.", DataType = "Decimal", ExampleValue = "350000" },
            new() { FieldName = "modified_interest_rate", DisplayName = "Modified Interest Rate", Description = "Modified interest rate.", DataType = "Decimal", ExampleValue = "4.5" },
            new() { FieldName = "original_rate", DisplayName = "Original Interest Rate", Description = "Original interest rate before modification.", DataType = "Decimal", ExampleValue = "4.5" },
            new() { FieldName = "interest_rate_before", DisplayName = "Interest Rate Before", Description = "Interest rate before modification.", DataType = "Decimal", ExampleValue = "4.5" },
            new() { FieldName = "cap_amount", DisplayName = "Capitalized Amount", Description = "Capitalized amount for modifications.", DataType = "Decimal", ExampleValue = "5000" },
            new() { FieldName = "cap_amounts", DisplayName = "Capitalized Amounts List", Description = "Array/List of capitalized amounts.", DataType = "String", ExampleValue = "[10000, 15000]" },
            new() { FieldName = "prior_cap_amounts", DisplayName = "Prior Capitalized Amounts", Description = "Array/List of prior capitalized amounts.", DataType = "String", ExampleValue = "[3000, 20000]" },
            new() { FieldName = "current_mod_cap_amount", DisplayName = "Current Modified Capitalized Amount", Description = "Current modified capitalized amount.", DataType = "Decimal", ExampleValue = "0" },
            new() { FieldName = "new_first_payment_date", DisplayName = "New First Payment Date", Description = "New first payment date after modification.", DataType = "String", ExampleValue = "2026-06-01" },
            new() { FieldName = "original_maturity_date", DisplayName = "Original Maturity Date", Description = "Original maturity date of the mortgage.", DataType = "String", ExampleValue = "2036-06-01" },
            new() { FieldName = "new_maturity_date", DisplayName = "New Maturity Date", Description = "New maturity date after modification.", DataType = "String", ExampleValue = "2046-06-01" },
            new() { FieldName = "SecurityInstrumentDate_Mod", DisplayName = "Security Instrument Date (Mod)", Description = "Security instrument date on modification document.", DataType = "String", ExampleValue = "2020-05-01" },
            new() { FieldName = "DATED_DATE_MTG", DisplayName = "Dated Date (Mtg)", Description = "Dated date on mortgage document.", DataType = "String", ExampleValue = "2020-05-01" },
            new() { FieldName = "UNPAID_PRINCIPAL_BALANCE_COVER_LETTER", DisplayName = "Unpaid Principal Balance (Cover Letter)", Description = "Unpaid Principal Balance in cover letter.", DataType = "Decimal", ExampleValue = "150000.00" },
            new() { FieldName = "Unpaid_Principal_Balance_Mod", DisplayName = "Unpaid Principal Balance (Mod Underscore)", Description = "Unpaid Principal Balance on modification document.", DataType = "Decimal", ExampleValue = "150000.00" },
            new() { FieldName = "Capitalized_UPB_Summary_Report", DisplayName = "Capitalized UPB Summary Report (Underscore)", Description = "Capitalized UPB on summary report.", DataType = "Decimal", ExampleValue = "150000.00" },
            new() { FieldName = "MODIFIED_NOTE_RATE_COVER_LETTER", DisplayName = "Modified Note Rate (Cover Letter)", Description = "Modified note rate on cover letter.", DataType = "Decimal", ExampleValue = "4.5" },
            new() { FieldName = "Interest_Rate_Mod", DisplayName = "Interest Rate (Mod)", Description = "Modified interest rate on modification document.", DataType = "Decimal", ExampleValue = "4.5" },
            new() { FieldName = "Interest_Rate_Summary_Report", DisplayName = "Interest Rate Summary Report", Description = "Interest rate on summary report.", DataType = "Decimal", ExampleValue = "4.5" },
            new() { FieldName = "MONTHLY_PI_PAYMENT_COVER_LETTER", DisplayName = "Monthly P&I Payment (Cover Letter)", Description = "Monthly Principal & Interest payment on cover letter.", DataType = "Decimal", ExampleValue = "760.03" },
            new() { FieldName = "Monthly_Payment_Mod", DisplayName = "Monthly Payment (Mod)", Description = "Monthly payment amount on modification document.", DataType = "Decimal", ExampleValue = "760.03" },
            new() { FieldName = "Principal_And_Interest_Summary_Report", DisplayName = "Principal & Interest Summary Report", Description = "Principal & Interest payment amount on summary report.", DataType = "Decimal", ExampleValue = "760.03" },
            new() { FieldName = "MODIFIED_MATURITY_DATE_COVER_LETTER", DisplayName = "Modified Maturity Date (Cover Letter Underscore)", Description = "Modified maturity date on cover letter.", DataType = "String", ExampleValue = "2056-06-01" },
            new() { FieldName = "Maturity_Date_Mod_Mod", DisplayName = "Maturity Date (Mod Mod Underscore)", Description = "Maturity date on modification document.", DataType = "String", ExampleValue = "2056-06-01" },
            new() { FieldName = "Maturity_Date_Summary_Report", DisplayName = "Maturity Date Summary Report", Description = "Maturity date on summary report.", DataType = "String", ExampleValue = "2056-06-01" },
            new() { FieldName = "TOTAL_MONTHLY_PAYMENT_COVER_LETTER", DisplayName = "Total Monthly Payment (Cover Letter)", Description = "Total monthly payment on cover letter.", DataType = "Decimal", ExampleValue = "950.50" },
            new() { FieldName = "Total_Payment_Summary_Report", DisplayName = "Total Payment Summary Report", Description = "Total payment amount on summary report.", DataType = "Decimal", ExampleValue = "950.50" },
            new() { FieldName = "FIRST_MODIFIED_PAYMENT_DUE_DATE_COVER_LETTER", DisplayName = "First Modified Payment Due Date (Cover Letter)", Description = "First modified payment due date on cover letter.", DataType = "String", ExampleValue = "2026-07-01" },
            new() { FieldName = "First_Payment_Date_Mod", DisplayName = "First Payment Date (Mod)", Description = "First payment date on modification document.", DataType = "String", ExampleValue = "2026-07-01" },
            new() { FieldName = "Payment_Effective_Date_Summary_Report", DisplayName = "Payment Effective Date Summary Report", Description = "Payment effective date on summary report.", DataType = "String", ExampleValue = "2026-07-01" },
            new() { FieldName = "INSTRUMENT_NUMBER_MTG", DisplayName = "Instrument Number (Mtg)", Description = "Instrument number on mortgage document.", DataType = "String", ExampleValue = "987654321" },
            new() { FieldName = "ORIGINAL_INSTRUMENT_NUMBER_Mod", DisplayName = "Original Instrument Number (Mod)", Description = "Original instrument number referenced in modification document.", DataType = "String", ExampleValue = "987654321" },
            new() { FieldName = "RECORDED_DATE_MTG", DisplayName = "Recorded Date (Mtg)", Description = "Recording date of mortgage document.", DataType = "String", ExampleValue = "2020-05-15" },
            new() { FieldName = "ORIGINAL_RECORDING_DATE_Mod", DisplayName = "Original Recording Date (Mod)", Description = "Original recording date referenced in modification document.", DataType = "String", ExampleValue = "2020-05-15" },
            new() { FieldName = "LoanAmount_MTG", DisplayName = "Loan Amount (Mtg)", Description = "Loan amount on mortgage document.", DataType = "Decimal", ExampleValue = "150000.00" },
            new() { FieldName = "SecurityInstrumentAmount_Mod", DisplayName = "Security Instrument Amount (Mod)", Description = "Security instrument amount on modification document.", DataType = "Decimal", ExampleValue = "150000.00" },
            new() { FieldName = "LENDERNAME_MTG", DisplayName = "Lender Name (Mtg)", Description = "Lender name on mortgage document.", DataType = "String", ExampleValue = "WELLS FARGO BANK, N.A." },
            new() { FieldName = "LENDERNAME_MOD", DisplayName = "Lender Name (Mod)", Description = "Lender name on modification document.", DataType = "String", ExampleValue = "Wells Fargo Bank NA" },
            new() { FieldName = "TRUSTEE_NAME_MTG", DisplayName = "Trustee Name (Mtg)", Description = "Trustee name on mortgage document.", DataType = "String", ExampleValue = "FIDELITY NATIONAL TITLE" },
            new() { FieldName = "TRUSTEE_NAME_MOD", DisplayName = "Trustee Name (Mod)", Description = "Trustee name on modification document.", DataType = "String", ExampleValue = "Fidelity National Title" },
            new() { FieldName = "BORROWER_NAME1_MTG", DisplayName = "Borrower 1 Name (Mtg)", Description = "Primary borrower name on mortgage document.", DataType = "String", ExampleValue = "JOHN H. DOE" },
            new() { FieldName = "BORROWER_NAME1_MOD", DisplayName = "Borrower 1 Name (Mod)", Description = "Primary borrower name on modification document.", DataType = "String", ExampleValue = "John Doe" },
            new() { FieldName = "BORROWER_NAME2_MTG", DisplayName = "Borrower 2 Name (Mtg)", Description = "Secondary borrower name on mortgage document.", DataType = "String", ExampleValue = "JANE M. DOE" },
            new() { FieldName = "BORROWER_NAME2_MOD", DisplayName = "Borrower 2 Name (Mod)", Description = "Secondary borrower name on modification document.", DataType = "String", ExampleValue = "Jane Doe" },
            new() { FieldName = "EscrowShortage_Summary_Report", DisplayName = "Escrow Shortage Summary Report", Description = "Escrow shortage amount on summary report.", DataType = "Decimal", ExampleValue = "1200.00" },
            new() { FieldName = "EscrowShortage_COVER_LETTER", DisplayName = "Escrow Shortage (Cover Letter)", Description = "Escrow shortage amount on cover letter.", DataType = "Decimal", ExampleValue = "1200.00" },
            new() { FieldName = "MODA_Escrow_Shortage", DisplayName = "MODA Escrow Shortage", Description = "Escrow shortage on MODA calculation.", DataType = "Decimal", ExampleValue = "1200.00" },
            new() { FieldName = "EscrowShortage_Spread_Summary_Report", DisplayName = "Escrow Shortage Spread Summary Report", Description = "Escrow shortage spread on summary report.", DataType = "Decimal", ExampleValue = "100.00" },
            new() { FieldName = "EscrowPayment_Summary_Report", DisplayName = "Escrow Payment Summary Report", Description = "Escrow payment amount on summary report.", DataType = "Decimal", ExampleValue = "150.00" },
            new() { FieldName = "Monthly_Escrow_Payment_CoverLetter", DisplayName = "Monthly Escrow Payment (Cover Letter)", Description = "Monthly escrow payment on cover letter.", DataType = "Decimal", ExampleValue = "250.00" },
            new() { FieldName = "PrincipalForbearance_SummaryReport", DisplayName = "Principal Forbearance Summary Report", Description = "Principal forbearance on summary report.", DataType = "Decimal", ExampleValue = "25000.00" },
            new() { FieldName = "DefferedPrincipalBalance_Mod", DisplayName = "Deferred Principal Balance (Mod)", Description = "Deferred principal balance on modification document.", DataType = "Decimal", ExampleValue = "25000.00" },
            new() { FieldName = "ContributionTotal_SummaryReport", DisplayName = "Contribution Total Summary Report", Description = "Total cash contribution on summary report.", DataType = "Decimal", ExampleValue = "5000.00" },
            new() { FieldName = "CASH_CONTRIBUTION_COVER_LETTER", DisplayName = "Cash Contribution (Cover Letter)", Description = "Cash contribution amount on cover letter.", DataType = "Decimal", ExampleValue = "5000.00" },
            new() { FieldName = "LenderName_ASN", DisplayName = "Lender Name (Asn)", Description = "Lender name on assignment document.", DataType = "String", ExampleValue = "WELLS FARGO BANK, N.A." },
            new() { FieldName = "LenderAcknowledgement_Mod", DisplayName = "Lender Acknowledgement (Mod)", Description = "Lender name in acknowledgement block of modification.", DataType = "String", ExampleValue = "WELLS FARGO BANK N.A." },
            new() { FieldName = "PropertyCounty", DisplayName = "Property County", Description = "County where the property is located.", DataType = "String", ExampleValue = "Carbon" }
        ];
    }

    // ─── Layer 1: Dynamic Keyword Filtering ─────────────────────────────────────
    public string GetOptimizedSchemaText(string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
            return MinifyFields(GetSchema());

        var allFields = GetSchema();
        var promptLower = userPrompt.ToLowerInvariant();

        // Punctuation separators to clean tokens consistently
        char[] separators = [' ', ',', '.', ';', ':', '(', ')', '"', '\'', '?', '!', '[', ']', '{', '}'];

        // Tokenize the user prompt into meaningful words
        var promptWords = promptLower
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
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
                .Split(separators, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2);
            var descTokens = field.Description.ToLowerInvariant()
                .Split(separators, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2);

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
- SIMPLE request (no conditional nesting and no comment/failure reason) → use FLAT format.
- CONDITIONAL request (""only if"", ""when X then check Y"", ""for X loans: check..."") or ANY request specifying a comment/failure reason → use NESTED format.

SUCCESS, FAILURE, AND COMMENT FIELDS:
1. successMessage: Top-level message returned if the rule evaluates to true.
2. failureMessage: Message returned if the rule evaluates to false.
3. comment: If a user specifies a comment (e.g. ""Comment: 'reason'""), this MUST be stored in the child sub-rule's ""failureMessage"" field, and the rule MUST be structured as NESTED (parent rule + child sub-rules), even for a single logic condition. This allows the engine to bubble up the failed sub-rule's failureMessage as the parent rule's comment.

FLAT FORMAT (leaf rule — no sub-rules):
{
  ""ruleName"": ""<descriptive rule name>"",
  ""expression"": ""<single boolean C# expression>"",
  ""successMessage"": ""<success message if specified>"",
  ""failureMessage"": ""<failure message if specified>"",
  ""sampleJson"": { ""Field1"": value1, ""Field2"": value2 }
}

NESTED FORMAT (with sub-rules):
The parent rule has NO expression (it is ignored by the engine). The gate condition
is the FIRST child in ""rules"". All checks follow as additional child rules.
{
  ""ruleName"": ""<descriptive rule name>"",
  ""expression"": null,
  ""operator"": ""<And|Or>"",
  ""successMessage"": ""<success message if specified>"",
  ""failureMessage"": ""<failure message if specified>"",
  ""rules"": [
    { 
      ""ruleName"": ""<sub-rule A name>"", 
      ""expression"": ""<expr A>"",
      ""successMessage"": null,
      ""failureMessage"": ""<comment/failure reason for A if specified>""
    }
  ],
  ""sampleJson"": { ""Field1"": value1, ""Field2"": value2 }
}

EXAMPLES:

Input: ""Verify that borrower credit score is >= 680. Success: 'Credit Approved', Failure: 'Credit Rejected', Comment: 'Borrower credit score is below minimum threshold of 680'""
Output JSON:
{
  ""ruleName"": ""Credit Score Check"",
  ""expression"": null,
  ""operator"": ""And"",
  ""successMessage"": ""Credit Approved"",
  ""failureMessage"": ""Credit Rejected"",
  ""rules"": [
    {
      ""ruleName"": ""Verify Minimum Credit"",
      ""expression"": ""Borrower1CreditScore >= 680"",
      ""failureMessage"": ""Borrower credit score is below minimum threshold of 680""
    }
  ],
  ""sampleJson"": {
    ""Borrower1CreditScore"": 700
  }
}

Input: ""Credit score at least 620 and LTV under 80""
Output JSON:
{
  ""ruleName"": ""Credit and LTV Check"",
  ""expression"": ""Borrower1CreditScore >= 620 && ORIG_LTV < 80"",
  ""successMessage"": null,
  ""failureMessage"": null,
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
