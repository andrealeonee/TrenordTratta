using System.Text.Json;
using TrenordTratta.Core.Services;

namespace TrenordTratta.Core.Api.Trenord;

public interface ITrenordClient
{
    /// <summary>Restituisce tutti i risultati per il numero treno (possono essere più treni/percorsi).</summary>
    Task<IReadOnlyList<TrenordResult>> GetTrainAsync(string trainNumber, CancellationToken ct = default);
}

/// <summary>
/// GET https://admin.trenord.it/store-management-api/mia/train/{numeroTreno}/
/// Nessuna autenticazione. Il retry con backoff è configurato sull'HttpClient (vedi ServiceCollectionExtensions).
/// </summary>
public sealed class TrenordClient(HttpClient http) : ITrenordClient
{
    public const string BaseAddress = "https://admin.trenord.it/store-management-api/mia/";
    private const string ApiName = "Trenord";

    public async Task<IReadOnlyList<TrenordResult>> GetTrainAsync(string trainNumber, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(trainNumber) || !trainNumber.All(char.IsDigit))
            throw new ArgumentException("Numero treno non valido", nameof(trainNumber));

        using var response = await http.GetAsync($"train/{trainNumber}/", ct).ConfigureAwait(false);
        var body = await ApiResponse.ReadCheckedAsync(response, ApiName, ct).ConfigureAwait(false);
        return Parse(body);
    }

    internal static IReadOnlyList<TrenordResult> Parse(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<List<TrenordResult>>(body, FlexibleJson.Options) ?? [];
        }
        catch (JsonException ex)
        {
            throw new ApiException(ApiName, "JSON non valido", inner: ex);
        }
    }
}

public static class TrenordJourneySelector
{
    /// <summary>
    /// Sceglie il viaggio giusto:
    /// 1) train.date == giorno richiesto (yyyyMMdd) e pass_list non vuota;
    /// 2) tra questi preferisce quelli che passano dalla stazione d'origine della tratta;
    /// 3) poi quelli con almeno una fermata con actual_data (regola del repo);
    /// se nessuno ha actual_data (treno non ancora partito) prende comunque il primo valido.
    /// </summary>
    public static TrenordJourney? Select(IEnumerable<TrenordResult> results, DateTime day, string? originName = null)
    {
        var dateKey = day.ToString("yyyyMMdd");

        var candidates = results
            .SelectMany(r => r.JourneyList ?? [])
            .Where(j => j.Train?.Date == dateKey && j.PassList is { Count: > 0 })
            .ToList();

        if (candidates.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(originName))
        {
            var withOrigin = candidates
                .Where(j => j.PassList!.Any(p => StationNameMatcher.Matches(p.Station?.StationOriName, originName)))
                .ToList();
            if (withOrigin.Count > 0) candidates = withOrigin;
        }

        return candidates.FirstOrDefault(j => j.PassList!.Any(p => p.ActualData is not null))
               ?? candidates[0];
    }
}
