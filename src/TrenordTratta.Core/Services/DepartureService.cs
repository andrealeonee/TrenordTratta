using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrenordTratta.Core.Api;
using TrenordTratta.Core.Api.Trenord;
using TrenordTratta.Core.Api.ViaggiaTreno;
using TrenordTratta.Core.Models;
using TrenordTratta.Core.Settings;

namespace TrenordTratta.Core.Services;

public sealed record DepartureBoard(RouteLeg Leg, IReadOnlyList<Departure> Departures, IReadOnlyList<string> Warnings, DateTime FetchedAt);

public interface IDepartureService
{
    Task<DepartureBoard> GetDeparturesAsync(RouteLeg leg, AppSettings settings, CancellationToken ct = default);

    /// <summary>Ricarica un singolo treno (pagina di dettaglio). Null se non più disponibile.</summary>
    Task<Departure?> RefreshAsync(Departure departure, RouteLeg leg, AppSettings settings, CancellationToken ct = default);
}

/// <summary>
/// Flusso:
/// 1. partenze dalla stazione d'origine (ViaggiaTreno), su più finestre per coprire il lookahead;
/// 2. per ogni treno Trenord (codiceCliente 63) dettaglio da Trenord: fermate, orari reali, affollamento;
/// 3. si tengono solo i treni che fermano a destinazione DOPO l'origine (esclude la direzione opposta);
/// 4. merge sulla tripla numero + origine + giorno.
/// </summary>
public sealed class DepartureService(
    IViaggiaTrenoClient viaggiaTreno,
    ITrenordClient trenord,
    ILogger<DepartureService>? logger = null,
    Func<DateTime>? clock = null) : IDepartureService
{
    public const int TrenordClientCode = 63;

    private const int MaxParallelRequests = 4;
    private static readonly TimeSpan PastWindow = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan WindowStep = TimeSpan.FromMinutes(60);

    private readonly ILogger _log = (ILogger?)logger ?? NullLogger.Instance;
    private readonly Func<DateTime> _now = clock ?? ViaggiaTrenoDate.NowRome;

    public async Task<DepartureBoard> GetDeparturesAsync(RouteLeg leg, AppSettings settings, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(leg.FromCode))
            throw new InvalidOperationException($"Codice stazione mancante per {leg.FromName}");

        var now = _now();
        var warnings = new List<string>();
        var raw = await FetchVtWindowsAsync(leg.FromCode, now, settings.LookAheadMinutes, warnings, ct).ConfigureAwait(false);

        // Dedup sulla tripla numero + origine + giorno, ordinamento per orario.
        var candidates = raw
            .Where(d => !string.IsNullOrWhiteSpace(d.NumeroTreno) && d.OrarioPartenza is not null)
            .GroupBy(d => KeyOf(d))
            .Select(g => g.First())
            .OrderBy(d => d.OrarioPartenza)
            .Where(d => ViaggiaTrenoDate.FromUnixMs(d.OrarioPartenza!.Value) >= now - PastWindow)
            .Take(settings.MaxTrains * 3) // margine: parte dei treni va nell'altra direzione
            .ToList();

        using var gate = new SemaphoreSlim(MaxParallelRequests);
        var unverified = 0;
        var trenordFailures = 0;

        var tasks = candidates.Select(async vt =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var result = await EnrichAsync(vt, leg, settings, ct).ConfigureAwait(false);
                if (result.TrenordFailed) Interlocked.Increment(ref trenordFailures);
                if (result.Unverified) Interlocked.Increment(ref unverified);
                return result.Departure;
            }
            finally
            {
                gate.Release();
            }
        });

        var enriched = await Task.WhenAll(tasks).ConfigureAwait(false);

        if (trenordFailures > 0)
            warnings.Add($"Dati Trenord non disponibili per {trenordFailures} treni: orari/affollamento da ViaggiaTreno.");
        if (unverified > 0)
            warnings.Add($"{unverified} treni esclusi: impossibile verificare la fermata a {leg.ToName}.");

        var list = enriched
            .OfType<Departure>()
            .Where(d => !d.HasDeparted)
            .OrderBy(d => d.ScheduledDeparture)
            .Take(settings.MaxTrains)
            .ToList();

        return new DepartureBoard(leg, list, warnings, now);
    }

    public async Task<Departure?> RefreshAsync(Departure departure, RouteLeg leg, AppSettings settings, CancellationToken ct = default)
    {
        if (departure.VtSource is null) return null;
        var result = await EnrichAsync(departure.VtSource, leg, settings, ct).ConfigureAwait(false);
        return result.Departure;
    }

    private async Task<List<VtDeparture>> FetchVtWindowsAsync(string code, DateTime now, int lookAheadMinutes, List<string> warnings, CancellationToken ct)
    {
        var starts = new List<DateTime>();
        for (var t = now - PastWindow; t <= now + TimeSpan.FromMinutes(Math.Max(lookAheadMinutes, 30)); t += WindowStep)
            starts.Add(t);

        var results = await Task.WhenAll(starts.Select(async t =>
        {
            try
            {
                var offset = new DateTimeOffset(t, ViaggiaTrenoDate.Rome.GetUtcOffset(t));
                return (Ok: true, Items: await viaggiaTreno.GetDeparturesAsync(code, offset, ct).ConfigureAwait(false));
            }
            catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                _log.LogWarning(ex, "Partenze ViaggiaTreno fallite per {Time}", t);
                return (Ok: false, Items: (IReadOnlyList<VtDeparture>)[]);
            }
        })).ConfigureAwait(false);

        if (results.All(r => !r.Ok))
            throw new ApiException("ViaggiaTreno", "Impossibile recuperare le partenze");
        if (results.Any(r => !r.Ok))
            warnings.Add("Alcune fasce orarie non sono state caricate.");

        return results.SelectMany(r => r.Items).ToList();
    }

    private static DepartureKey KeyOf(VtDeparture d) => new(
        d.NumeroTreno!.Trim(),
        d.CodOrigine ?? "",
        ViaggiaTrenoDate.FromUnixMs(d.DataPartenzaTreno ?? d.OrarioPartenza ?? 0).Date);

    private sealed record EnrichResult(Departure? Departure, bool Unverified, bool TrenordFailed);

    private async Task<EnrichResult> EnrichAsync(VtDeparture vt, RouteLeg leg, AppSettings settings, CancellationToken ct)
    {
        var number = vt.NumeroTreno!.Trim();
        var scheduled = ViaggiaTrenoDate.FromUnixMs(vt.OrarioPartenza!.Value);
        var day = ViaggiaTrenoDate.FromUnixMs(vt.DataPartenzaTreno ?? vt.OrarioPartenza!.Value).Date;
        var isTrenord = vt.ClientCodeResolved == TrenordClientCode;

        // --- 1. Trenord
        TrenordJourney? journey = null;
        var trenordFailed = false;
        if (isTrenord)
        {
            try
            {
                var results = await trenord.GetTrainAsync(number, ct).ConfigureAwait(false);
                journey = TrenordJourneySelector.Select(results, day, leg.FromName);
            }
            catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                _log.LogWarning(ex, "Trenord fallito per treno {Number}", number);
                trenordFailed = true;
            }
        }

        // --- 2. Fermate: Trenord, con fallback ViaggiaTreno per stazioni mancanti o Trenord assente
        List<StopInfo> stops = journey is not null ? MapTrenordStops(journey, day) : [];
        var needsVt = stops.Count == 0 || stops.Any(s => s.StationName.Length == 0);
        List<StopInfo>? vtStops = null;
        if (needsVt && vt.DataPartenzaTreno is long depMs && !string.IsNullOrWhiteSpace(vt.CodOrigine))
        {
            try
            {
                var status = await viaggiaTreno.GetTrainStatusAsync(vt.CodOrigine, number, depMs, ct).ConfigureAwait(false);
                vtStops = MapVtStops(status);
            }
            catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                _log.LogDebug(ex, "andamentoTreno fallito per {Number}", number);
            }
        }

        if (vtStops is { Count: > 0 })
            stops = stops.Count == 0 ? vtStops : FillMissingStations(stops, vtStops);

        // --- 3. Verifica fermata a destinazione dopo l'origine
        var originIdx = IndexOfStation(stops, leg.FromName, leg.FromCode);
        var destIdx = originIdx >= 0 ? IndexOfStation(stops, leg.ToName, leg.ToCode, startAt: originIdx + 1) : -1;

        bool verified;
        if (originIdx >= 0 && destIdx > originIdx) verified = true;
        else if (stops.Count > 0 && originIdx >= 0) return new(null, false, trenordFailed); // va nell'altra direzione o non ferma
        else verified = StationNameMatcher.Matches(vt.Destinazione, leg.ToName); // ultimo tentativo: il capolinea è la destinazione

        if (!verified) return new(null, true, trenordFailed);

        var originStop = originIdx >= 0 ? stops[originIdx] : null;
        var destStop = destIdx >= 0 ? stops[destIdx] : null;

        // --- 4. Merge dei dati
        var dep = new Departure
        {
            TrainNumber = number,
            Category = vt.Categoria ?? vt.CategoriaDescrizione ?? "",
            OriginCode = vt.CodOrigine ?? "",
            TrainDate = day,
            Terminus = vt.Destinazione ?? "",
            IsTrenord = isTrenord,
            ScheduledDeparture = originStop?.ScheduledDeparture ?? scheduled,
            VtSource = vt,
            Stops = stops,
            DestinationVerified = true,
        };

        dep.ActualDeparture = originStop?.ActualDeparture;
        dep.HasDeparted = dep.ActualDeparture is not null && dep.ActualDeparture < _now().AddMinutes(-1);
        dep.DelayMinutes = ComputeDelay(stops, originIdx, vt);

        var vtPlatform = string.IsNullOrWhiteSpace(vt.BinarioEffettivo) ? null : vt.BinarioEffettivo;
        dep.Platform = vtPlatform ?? originStop?.Platform ?? vt.BinarioProgrammato;
        dep.PlatformConfirmed = vtPlatform is not null;

        dep.ScheduledArrival = destStop?.ScheduledArrival ?? destStop?.ScheduledDeparture;
        dep.ActualArrival = destStop?.ActualArrival;

        dep.IsCancelled = vt.Provvedimento == 1
                          || originStop?.IsCancelled == true
                          || destStop?.IsCancelled == true;

        var crowding = journey?.Train?.Crowding;
        dep.CrowdingSource = crowding?.Source;
        if (crowding?.Percentage is int pct && settings.IsCrowdingSourceTrusted(crowding.Source))
            dep.CrowdingPercent = Math.Clamp(pct, 0, 100);

        return new(dep, false, trenordFailed);
    }

    /// <summary>
    /// Ritardo: 1) partenza reale dall'origine; 2) ultima fermata già transitata; 3) ritardo di ViaggiaTreno.
    /// </summary>
    internal static int? ComputeDelay(IReadOnlyList<StopInfo> stops, int originIdx, VtDeparture vt)
    {
        if (originIdx >= 0 && stops[originIdx].DelayMinutes is int atOrigin) return atOrigin;

        var lastPassed = stops.LastOrDefault(s => s.DelayMinutes is not null);
        if (lastPassed?.DelayMinutes is int last) return last;

        return vt.Ritardo;
    }

    internal static List<StopInfo> MapTrenordStops(TrenordJourney journey, DateTime day)
    {
        var result = new List<StopInfo>();
        DateTime? reference = null;

        foreach (var p in journey.PassList ?? [])
        {
            var schedArr = ViaggiaTrenoDate.ParseTime(p.ArrTime, day, reference);
            var schedDep = ViaggiaTrenoDate.ParseTime(p.DepTime, day, reference);
            reference = schedDep ?? schedArr ?? reference;

            var type = (p.ActualData?.ActualType ?? p.Type)?.Trim().ToUpperInvariant() switch
            {
                "O" => StopType.Origin,
                "D" => StopType.Destination,
                _ => StopType.Stop,
            };

            result.Add(new StopInfo
            {
                StationName = p.Station?.StationOriName?.Trim() ?? "",
                StationCode = p.Station?.StationId ?? p.ActualData?.ActualStationMir,
                Type = type,
                ScheduledArrival = type == StopType.Origin ? null : schedArr,
                ScheduledDeparture = type == StopType.Destination ? null : schedDep,
                ActualArrival = ViaggiaTrenoDate.ParseTime(p.ActualData?.ArrActualTime, day, schedArr ?? reference),
                ActualDeparture = ViaggiaTrenoDate.ParseTime(p.ActualData?.DepActualTime, day, schedDep ?? reference),
                Platform = string.IsNullOrWhiteSpace(p.Platform) ? null : p.Platform.Trim(),
                IsCancelled = p.Cancelled,
            });
        }

        return result;
    }

    internal static List<StopInfo> MapVtStops(VtTrainStatus? status)
    {
        if (status?.Fermate is not { Count: > 0 } fermate) return [];

        return fermate.Select(f => new StopInfo
        {
            StationName = f.Stazione?.Trim() ?? "",
            StationCode = f.Id,
            Type = f.TipoFermata switch { "P" => StopType.Origin, "A" => StopType.Destination, _ => StopType.Stop },
            ScheduledArrival = f.ArrivoTeorico is long a and > 0 ? ViaggiaTrenoDate.FromUnixMs(a) : null,
            ScheduledDeparture = f.PartenzaTeorica is long p and > 0 ? ViaggiaTrenoDate.FromUnixMs(p) : null,
            ActualArrival = f.ArrivoReale is long ar and > 0 ? ViaggiaTrenoDate.FromUnixMs(ar) : null,
            ActualDeparture = f.PartenzaReale is long pr and > 0 ? ViaggiaTrenoDate.FromUnixMs(pr) : null,
            Platform = f.BinarioEffettivo ?? f.BinarioProgrammato,
            IsCancelled = f.ActualFermataType == 3,
            FromFallback = true,
        }).ToList();
    }

    /// <summary>
    /// Fermate Trenord senza "station": si prende il nome da ViaggiaTreno.
    /// Mappatura posizionale solo se le due liste hanno la stessa lunghezza, altrimenti si usa tutta la lista VT.
    /// </summary>
    internal static List<StopInfo> FillMissingStations(List<StopInfo> trenordStops, List<StopInfo> vtStops)
    {
        if (trenordStops.Count != vtStops.Count) return vtStops;

        return trenordStops.Select((s, i) => s.StationName.Length > 0
            ? s
            : new StopInfo
            {
                StationName = vtStops[i].StationName,
                StationCode = vtStops[i].StationCode,
                Type = s.Type,
                ScheduledArrival = s.ScheduledArrival ?? vtStops[i].ScheduledArrival,
                ScheduledDeparture = s.ScheduledDeparture ?? vtStops[i].ScheduledDeparture,
                ActualArrival = s.ActualArrival ?? vtStops[i].ActualArrival,
                ActualDeparture = s.ActualDeparture ?? vtStops[i].ActualDeparture,
                Platform = s.Platform ?? vtStops[i].Platform,
                IsCancelled = s.IsCancelled,
                FromFallback = true,
            }).ToList();
    }

    internal static int IndexOfStation(IReadOnlyList<StopInfo> stops, string name, string? code, int startAt = 0)
    {
        for (var i = startAt; i < stops.Count; i++)
        {
            var s = stops[i];
            if (!string.IsNullOrWhiteSpace(code) && string.Equals(s.StationCode, code, StringComparison.OrdinalIgnoreCase))
                return i;
            if (StationNameMatcher.Matches(s.StationName, name))
                return i;
        }
        return -1;
    }
}
