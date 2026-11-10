using System.Text.Json;
using TrenordTratta.Core.Services;

namespace TrenordTratta.Core.Api.ViaggiaTreno;

public interface IViaggiaTrenoClient
{
    Task<IReadOnlyList<VtDeparture>> GetDeparturesAsync(string stationCode, DateTimeOffset when, CancellationToken ct = default);
    Task<VtTrainStatus?> GetTrainStatusAsync(string originCode, string trainNumber, long departureDateMs, CancellationToken ct = default);
    Task<IReadOnlyList<VtStation>> SearchStationsAsync(string text, CancellationToken ct = default);
}

/// <summary>
/// ViaggiaTreno (RFI). ATTENZIONE: solo HTTP, niente TLS.
/// Su Android serve l'eccezione cleartext limitata a www.viaggiatreno.it (network_security_config.xml).
/// </summary>
public sealed class ViaggiaTrenoClient(HttpClient http) : IViaggiaTrenoClient
{
    public const string BaseAddress = "http://www.viaggiatreno.it/infomobilita/resteasy/viaggiatreno/";
    private const string ApiName = "ViaggiaTreno";

    public async Task<IReadOnlyList<VtDeparture>> GetDeparturesAsync(string stationCode, DateTimeOffset when, CancellationToken ct = default)
    {
        var date = Uri.EscapeDataString(ViaggiaTrenoDate.Format(when));
        var body = await GetCheckedAsync($"partenze/{Uri.EscapeDataString(stationCode)}/{date}", ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body)) return [];
        return Deserialize<List<VtDeparture>>(body) ?? [];
    }

    public async Task<VtTrainStatus?> GetTrainStatusAsync(string originCode, string trainNumber, long departureDateMs, CancellationToken ct = default)
    {
        var body = await GetCheckedAsync(
            $"andamentoTreno/{Uri.EscapeDataString(originCode)}/{Uri.EscapeDataString(trainNumber)}/{departureDateMs}", ct).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(body) ? null : Deserialize<VtTrainStatus>(body);
    }

    /// <summary>autocompletaStazione: risposta testuale, una riga "NOME|CODICE" per stazione.</summary>
    public async Task<IReadOnlyList<VtStation>> SearchStationsAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 2) return [];
        var body = await GetCheckedAsync($"autocompletaStazione/{Uri.EscapeDataString(text.Trim())}", ct).ConfigureAwait(false);
        return ParseStations(body);
    }

    internal static IReadOnlyList<VtStation> ParseStations(string body) =>
        body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.Split('|'))
            .Where(p => p.Length == 2 && p[1].Length > 0)
            .Select(p => new VtStation(p[0].Trim(), p[1].Trim()))
            .ToList();

    private async Task<string> GetCheckedAsync(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct).ConfigureAwait(false);
        return await ApiResponse.ReadCheckedAsync(response, ApiName, ct).ConfigureAwait(false);
    }

    internal static T? Deserialize<T>(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body, FlexibleJson.Options);
        }
        catch (JsonException ex)
        {
            throw new ApiException(ApiName, "JSON non valido", inner: ex);
        }
    }
}
