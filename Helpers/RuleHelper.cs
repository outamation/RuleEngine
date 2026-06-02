using System;
using System.Collections.Generic;
using System.Linq;

namespace DemoRuleEngine.Helpers;

/// <summary>
/// Static helper methods for calculations and string matching used within rule expressions.
/// </summary>
public static class RuleHelper
{
    /// <summary>
    /// Calculates the number of whole months between two dates.
    /// Safely handles dynamic (object) inputs.
    /// </summary>
    public static int MonthsDifference(object? startDate, object? endDate)
    {
        var start = ToDateTime(startDate);
        var end = ToDateTime(endDate);

        if (start == null || end == null) return -1;

        return ((end.Value.Year - start.Value.Year) * 12) + end.Value.Month - start.Value.Month;
    }

    /// <summary>
    /// Calculates the number of days between two dates.
    /// Safely handles dynamic (object) inputs.
    /// </summary>
    public static int DaysDifference(object? startDate, object? endDate)
    {
        var start = ToDateTime(startDate);
        var end = ToDateTime(endDate);

        if (start == null || end == null) return -1;

        return (int)(end.Value.Date - start.Value.Date).TotalDays;
    }

    /// <summary>
    /// Checks if a nullable date has a value.
    /// </summary>
    public static bool HasValue(object? date)
    {
        return ToDateTime(date).HasValue;
    }

    /// <summary>
    /// Safely converts a dynamic object to a decimal for rule evaluation.
    /// </summary>
    public static decimal num(object? value)
    {
        if (value == null || value == DBNull.Value) return 0;
        try {
            return Convert.ToDecimal(value);
        } catch {
            return 0;
        }
    }

    /// <summary>
    /// Safely sums the numeric values of a dynamic collection/array.
    /// Safely handles dynamic (object) list/array inputs from JSON.
    /// </summary>
    public static decimal Sum(object? collection)
    {
        if (collection == null || collection == DBNull.Value) return 0;
        
        decimal total = 0;
        if (collection is System.Collections.IEnumerable enumerable && !(collection is string))
        {
            foreach (var item in enumerable)
            {
                total += num(item);
            }
        }
        else
        {
            total = num(collection);
        }
        
        return total;
    }

    /// <summary>
    /// Helper to convert dynamic object to DateTime.
    /// </summary>
    private static DateTime? ToDateTime(object? value)
    {
        if (value == null || value == DBNull.Value) return null;
        if (value is DateTime dt) return dt;

        if (DateTime.TryParse(value.ToString(), out var result))
            return result;

        return null;
    }

    /// <summary>
    /// Checks if the given value matches any of the allowed string values (case-insensitive, trimmed).
    /// Usage in expression: RuleHelper.In(value, "Option1", "Option2")
    /// </summary>
    public static bool In(object? value, params string[] allowedValues)
    {
        if (value == null || allowedValues == null || allowedValues.Length == 0) return false;
        
        var strValue = value.ToString()?.Trim();
        if (string.IsNullOrEmpty(strValue)) return false;

        foreach (var allowed in allowedValues)
        {
            if (strValue.Equals(allowed?.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Performs a case-sensitive, trimmed exact match between two dynamic values.
    /// Usage in expression: RuleHelper.ExactMatch(occurrence, expectedValue)
    /// </summary>
    public static bool ExactMatch(object? a, object? b)
    {
        return a?.ToString()?.Trim()
            .Equals(
                b?.ToString()?.Trim()
            ) ?? false;
    }

    /// <summary>
    /// Abbreviation-to-canonical-form mapping for common business and mortgage terms.
    /// Each abbreviation maps to its full form so that both sides normalize to the same token.
    /// </summary>
    private static readonly Dictionary<string, string> AbbreviationMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Business entity types
        { "CORP", "CORPORATION" },
        { "ASSN", "ASSOCIATION" },
        { "ASSOC", "ASSOCIATION" },
        { "INC", "INCORPORATED" },
        { "LTD", "LIMITED" },
        { "CO", "COMPANY" },

        // Mortgage / finance abbreviations
        { "MTG", "MORTGAGE" },
        { "NATL", "NATIONAL" },
        { "FED", "FEDERAL" },
        { "GOVT", "GOVERNMENT" },
        { "SVS", "SERVICES" },
        { "SVCS", "SERVICES" },
        { "SVC", "SERVICE" },
        { "BNKG", "BANKING" },
        { "BK", "BANK" },
        { "FINL", "FINANCIAL" },
        { "FIN", "FINANCIAL" },
        { "INTL", "INTERNATIONAL" },
        { "INS", "INSURANCE" },
        { "GRP", "GROUP" },
        { "MGMT", "MANAGEMENT" },
        { "CTR", "CENTER" },
        { "DEPT", "DEPARTMENT" },
        { "DEV", "DEVELOPMENT" },
        { "INVS", "INVESTMENTS" },
        { "INV", "INVESTMENT" },
        { "PROP", "PROPERTY" },
        { "PROPS", "PROPERTIES" },
        { "RLTY", "REALTY" },
        { "HLDGS", "HOLDINGS" },
        { "TR", "TRUST" },
    };

    /// <summary>
    /// Tokens that are pure legal suffixes and carry no semantic meaning for matching.
    /// These are removed entirely after abbreviation expansion.
    /// </summary>
    private static readonly HashSet<string> NoiseSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "LLC", "LLP", "LP", "NA", "N.A.", "FSB", "FA",
    };

    /// <summary>
    /// Performs a fuzzy match by tokenizing the strings, expanding abbreviations to canonical forms,
    /// removing noise-only suffixes, and comparing the resulting normalized token sequences.
    /// Also allows soft matching if one token list is a contiguous subset of the other (e.g. Borrower names).
    /// </summary>
    public static bool FuzzyMatch(object? a, object? b)
    {
        if (a == null || b == null) return false;

        var tokensA = NormalizeToTokens(a.ToString());
        var tokensB = NormalizeToTokens(b.ToString());

        if (tokensA.Count == 0 && tokensB.Count == 0) return true;
        if (tokensA.Count == 0 || tokensB.Count == 0) return false;

        // Exact match after normalization
        if (tokensA.SequenceEqual(tokensB, StringComparer.OrdinalIgnoreCase))
            return true;

        // Containment match: one token list is a contiguous sub-sequence of the other
        return IsSubSequence(tokensA, tokensB) || IsSubSequence(tokensB, tokensA);
    }

    /// <summary>
    /// Tokenizes the input, strips punctuation, expands abbreviations, and removes noise suffixes.
    /// Returns a list of canonical, uppercased tokens.
    /// </summary>
    private static List<string> NormalizeToTokens(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new List<string>();

        // Strip punctuation/symbols from each character, but preserve spaces & letters/digits
        var cleaned = new string(value.Select(c =>
            char.IsPunctuation(c) || char.IsSymbol(c) ? ' ' : c
        ).ToArray());

        var tokens = cleaned
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToUpperInvariant())
            .ToList();

        // Expand abbreviations and remove noise tokens
        var result = new List<string>();
        foreach (var token in tokens)
        {
            if (NoiseSuffixes.Contains(token))
                continue;

            if (AbbreviationMap.TryGetValue(token, out var expanded))
                result.Add(expanded.ToUpperInvariant());
            else
                result.Add(token);
        }

        return result;
    }

    /// <summary>
    /// Checks if 'shorter' is a contiguous sub-sequence within 'longer'.
    /// </summary>
    private static bool IsSubSequence(List<string> shorter, List<string> longer)
    {
        if (shorter.Count > longer.Count) return false;

        for (int i = 0; i <= longer.Count - shorter.Count; i++)
        {
            bool match = true;
            for (int j = 0; j < shorter.Count; j++)
            {
                if (!shorter[j].Equals(longer[i + j], StringComparison.OrdinalIgnoreCase))
                {
                    match = false;
                    break;
                }
            }
            if (match) return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if any term occurrence in lmaTerms mismatches the corresponding expected value.
    /// Returns true if ANY mismatch is found (i.e., rule should FAIL).
    /// Usage in expression: RuleHelper.HasAnyTermMismatch(expectedTerms, lmaTerms)
    /// </summary>
    public static bool HasAnyTermMismatch(
        Dictionary<string, object> expectedTerms,
        Dictionary<string, List<object>> lmaTerms)
    {
        foreach (var expected in expectedTerms)
        {
            if (!lmaTerms.ContainsKey(expected.Key))
                return true;

            foreach (var occurrence in lmaTerms[expected.Key])
            {
                if (!ExactMatch(occurrence, expected.Value))
                    return true;
            }
        }

        return false;
    }
}
