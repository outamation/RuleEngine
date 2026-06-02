namespace DemoRuleEngine.Models;

public class RuleInput
{
    // --- System / Schema Fields (defined in SchemaService) ---
    public decimal? ORIG_LTV { get; set; }
    public decimal? Borrower1CreditScore { get; set; }
    public decimal? Days_Past_Due { get; set; }
    public string? Orig_Product { get; set; }
    public decimal? Calc_Loan_Age { get; set; }
    public string? MOD_DATE { get; set; }
    public string? LoanEvaluationDate { get; set; }
    public string? Delinquency_Flag { get; set; }
    public string? PropertyUsageTypeCode { get; set; }
    public string? LienTypeCode { get; set; }
    public string? PropertyTypeCode { get; set; }
    public decimal? PRPY_NBR_UNITS { get; set; }
    public string? Servicing_ARM_Indicator { get; set; }
    public decimal? CurrentInterestRate { get; set; }
    public decimal? ModifiedInterestRate { get; set; }
    public decimal? PrincipalandInterestPaymentAmount { get; set; }
    public decimal? ModifiedPrincipalandInterestPaymentAmount { get; set; }
    public string? Forbearance_Flag { get; set; }
    public string? Bankruptcy_Flag { get; set; }
    public string? Deceased_Flag { get; set; }
    public string? ChargeOff_Flag { get; set; }
    public string? LossMitigation_Flag { get; set; }

    // --- Dynamic / Loan / Rule Helper Specific Fields (from existing database rules) ---
    public string? DLQ1_Loan_type { get; set; }
    public string? DLQ1_Investor_Name { get; set; }
    public string? DLQ1_Property_State { get; set; }
    public string? DLQ1_Property_State_CoverLetter { get; set; }
    public decimal? CapitalizedUPB_SummaryReport { get; set; }
    public decimal? UnpaidPrincipalBalance_Mod { get; set; }
    public decimal? UnpaidPrincipalBalance_CoverLetter { get; set; }
    public string? MaturityDate_Mod_Mod { get; set; }
    public string? Modified_Maturity_Date_CoverLetter { get; set; }
    public string? MaturityDate_SummaryReport { get; set; }
    public string? OriginalMtg_MaturityDate { get; set; }
}
