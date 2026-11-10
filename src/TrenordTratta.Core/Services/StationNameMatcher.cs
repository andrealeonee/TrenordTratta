using System.Globalization;
using System.Text;

namespace TrenordTratta.Core.Services;

/// <summary>
/// I codici stazione di Trenord e ViaggiaTreno non coincidono, quindi il confronto tra le due fonti
/// avviene sul nome normalizzato (maiuscolo, senza accenti/punteggiatura).
/// </summary>
public static class StationNameMatcher
{
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";

        var decomposed = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : ' ');
        }

        return string.Join(' ', sb.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w is not ("STAZIONE" or "FN" or "FNM")));
    }

    public static bool Matches(string? a, string? b)
    {
        var na = Normalize(a);
        var nb = Normalize(b);
        if (na.Length == 0 || nb.Length == 0) return false;
        if (na == nb) return true;

        // "MARIANO C." vs "MARIANO COMENSE". Niente match per prefisso puro: "MILANO" ≠ "MILANO CADORNA".
        var shorter = na.Length <= nb.Length ? na : nb;
        var longer = na.Length <= nb.Length ? nb : na;
        return TokensPrefixMatch(shorter.Split(' '), longer.Split(' '));
    }

    /// <summary>Ogni parola della forma breve è prefisso della parola corrispondente (gestisce abbreviazioni "C").</summary>
    private static bool TokensPrefixMatch(string[] shortTokens, string[] longTokens)
    {
        if (shortTokens.Length != longTokens.Length || shortTokens[0] != longTokens[0]) return false;
        for (var i = 1; i < shortTokens.Length; i++)
            if (!longTokens[i].StartsWith(shortTokens[i], StringComparison.Ordinal)) return false;
        return true;
    }
}
