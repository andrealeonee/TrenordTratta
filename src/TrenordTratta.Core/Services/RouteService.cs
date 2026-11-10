using TrenordTratta.Core.Api.ViaggiaTreno;
using TrenordTratta.Core.Data;
using TrenordTratta.Core.Models;

namespace TrenordTratta.Core.Services;

public interface IRouteService
{
    /// <summary>Risolve e salva i codici ViaggiaTreno mancanti cercando le stazioni per nome.</summary>
    Task<RouteDefinition> EnsureCodesAsync(RouteDefinition route, CancellationToken ct = default);

    Task<IReadOnlyList<VtStation>> SearchStationsAsync(string text, CancellationToken ct = default);
}

public sealed class RouteService(IViaggiaTrenoClient viaggiaTreno, IRouteRepository routes) : IRouteService
{
    public async Task<RouteDefinition> EnsureCodesAsync(RouteDefinition route, CancellationToken ct = default)
    {
        if (route.HasCodes) return route;

        if (string.IsNullOrWhiteSpace(route.StationACode))
            route.StationACode = await ResolveAsync(route.StationAName, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(route.StationBCode))
            route.StationBCode = await ResolveAsync(route.StationBName, ct).ConfigureAwait(false);

        await routes.SaveAsync(route).ConfigureAwait(false);
        return route;
    }

    public Task<IReadOnlyList<VtStation>> SearchStationsAsync(string text, CancellationToken ct = default) =>
        viaggiaTreno.SearchStationsAsync(text, ct);

    private async Task<string> ResolveAsync(string name, CancellationToken ct)
    {
        var found = await viaggiaTreno.SearchStationsAsync(name, ct).ConfigureAwait(false);
        var match = PickBest(found, name)
            ?? throw new InvalidOperationException(
                $"Stazione \"{name}\" non trovata su ViaggiaTreno. Modifica la tratta e selezionala dall'elenco.");
        return match.Code;
    }

    internal static VtStation? PickBest(IReadOnlyList<VtStation> found, string name)
    {
        var normalized = StationNameMatcher.Normalize(name);
        return found.FirstOrDefault(s => StationNameMatcher.Normalize(s.Name) == normalized)
               ?? (found.Count(s => StationNameMatcher.Matches(s.Name, name)) == 1
                   ? found.First(s => StationNameMatcher.Matches(s.Name, name))
                   : null);
    }
}
