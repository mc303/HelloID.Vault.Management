using System.Globalization;
using System.Text;

namespace HelloID.Vault.Services.ActiveDirectory;

/// <summary>
/// Deterministic scoring engine for AD match recommendations.
/// Compares vault person fields against AD user attributes using
/// exact, normalized, and fuzzy (Levenshtein) comparisons.
/// </summary>
public static class AdMatchScorer
{
    /// <summary>
    /// Computes a 0-100 similarity score between two strings using
    /// exact, normalized, and Levenshtein-based comparison.
    /// </summary>
    public static double ScoreStrings(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        {
            return 0;
        }

        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        var normA = Normalize(a);
        var normB = Normalize(b);

        if (normA.Length == 0 || normB.Length == 0)
        {
            return 0;
        }

        if (normA == normB)
        {
            return 95;
        }

        return Similarity(normA, normB);
    }

    /// <summary>
    /// Scores two display names using token comparison so decorative parts
    /// (e.g. "Daniel Buonocore (7008)") do not skew the result.
    /// </summary>
    public static double ScoreDisplayNames(string? personName, string? adName)
    {
        if (string.IsNullOrWhiteSpace(personName) || string.IsNullOrWhiteSpace(adName))
        {
            return 0;
        }

        var tokensA = ExtractNameTokens(personName);
        var tokensB = ExtractNameTokens(adName);

        if (tokensA.Count == 0 || tokensB.Count == 0)
        {
            return ScoreStrings(personName, adName);
        }

        // For each person token, find the best-matching AD token
        var total = 0.0;
        foreach (var tokenA in tokensA)
        {
            var best = tokensB.Max(tokenB => ScoreStrings(tokenA, tokenB));
            total += best;
        }

        return total / tokensA.Count;
    }

    /// <summary>Splits a display name into normalized tokens, dropping parenthesized parts and pure numbers.</summary>
    public static List<string> ExtractNameTokens(string name)
    {
        // Remove parenthesized parts like "(7008)"
        var cleaned = System.Text.RegularExpressions.Regex.Replace(name, @"\([^)]*\)", " ");
        return cleaned
            .Split(new[] { ' ', '-', '.', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !t.All(char.IsDigit))
            .Select(Normalize)
            .Where(t => t.Length > 0)
            .ToList();
    }

    /// <summary>
    /// Normalizes a string for comparison: lowercased, diacritics stripped, punctuation removed.
    /// </summary>
    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var formD = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);

        foreach (var ch in formD)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            if (char.IsLetterOrDigit(ch) || ch == '@' || ch == '.')
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
        }

        return sb.ToString();
    }

    /// <summary>Levenshtein-based similarity ratio (0-100).</summary>
    public static double Similarity(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0)
        {
            return 100;
        }
        if (a.Length == 0 || b.Length == 0)
        {
            return 0;
        }

        var distance = Levenshtein(a, b);
        var maxLen = Math.Max(a.Length, b.Length);
        return (1.0 - (double)distance / maxLen) * 100;
    }

    private static int Levenshtein(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            prev[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[b.Length];
    }
}
