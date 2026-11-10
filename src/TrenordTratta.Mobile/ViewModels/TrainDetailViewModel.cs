using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TrenordTratta.Core.Data;
using TrenordTratta.Core.Models;
using TrenordTratta.Core.Services;
using TrenordTratta.Core.Settings;

namespace TrenordTratta.Mobile.ViewModels;

public partial class TrainDetailViewModel(
    IDepartureService departures,
    ITripRepository trips,
    ISettingsService settings,
    ILogger<TrainDetailViewModel> logger) : ObservableObject, IQueryAttributable
{
    private Departure? _departure;
    private RouteLeg? _leg;
    private RouteDefinition? _route;

    public ObservableCollection<StopItemViewModel> Stops { get; } = [];

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _routeText = "";
    [ObservableProperty] private string _departureText = "";
    [ObservableProperty] private string _arrivalText = "";
    [ObservableProperty] private string _delayText = "";
    [ObservableProperty] private Color _delayColor = Palette.Unknown;
    [ObservableProperty] private string _platformText = "";
    [ObservableProperty] private bool _showCrowding;
    [ObservableProperty] private string _crowdingText = "";
    [ObservableProperty] private Color _crowdingColor = Palette.Unknown;
    [ObservableProperty] private double _crowdingProgress;
    [ObservableProperty] private string _crowdingSourceText = "";
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private string _lastUpdate = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TakenButtonText))]
    private bool _isTaken;

    public string TakenButtonText => IsTaken ? "✓ Segnato come preso · annulla" : "Prendo questo treno";

    public int AutoRefreshSeconds => settings.Current.AutoRefreshSeconds;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _departure = query.TryGetValue("departure", out var d) ? d as Departure : null;
        _leg = query.TryGetValue("leg", out var l) ? l as RouteLeg : null;
        _route = query.TryGetValue("route", out var r) ? r as RouteDefinition : null;
        IsTaken = query.TryGetValue("taken", out var t) && t is true;

        if (_departure is not null) Bind(_departure);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_departure is null || _leg is null) return;
        IsRefreshing = true;
        try
        {
            var updated = await departures.RefreshAsync(_departure, _leg, settings.Current);
            if (updated is null)
            {
                ErrorMessage = "Dati del treno non più disponibili.";
                return;
            }

            _departure = updated;
            ErrorMessage = "";
            Bind(updated);
            if (IsTaken) await trips.UpdateLatestAsync([updated], ViaggiaTrenoDate.NowRome());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Refresh dettaglio fallito");
            ErrorMessage = "Aggiornamento non riuscito.";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task ToggleTakenAsync()
    {
        if (_departure is null || _leg is null || _route is null) return;
        try
        {
            if (IsTaken) await trips.UnmarkAsync(_departure.TrainNumber, _departure.TrainDate);
            else await trips.MarkTakenAsync(_departure, _route, _leg, ViaggiaTrenoDate.NowRome());
            IsTaken = !IsTaken;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Salvataggio viaggio fallito");
            ErrorMessage = "Non sono riuscito a salvare il viaggio.";
        }
    }

    private void Bind(Departure d)
    {
        var s = settings.Current;

        Title = $"{d.Category} {d.TrainNumber}".Trim();
        RouteText = _leg?.Label ?? "";
        DepartureText = d.DelayMinutes is > 0 && !d.IsCancelled
            ? $"{d.ScheduledDeparture:HH:mm} → {d.ExpectedDeparture:HH:mm}"
            : d.ScheduledDeparture.ToString("HH:mm");
        ArrivalText = d.ScheduledArrival is DateTime a
            ? (d.ActualArrival is DateTime aa ? $"{a:HH:mm} (reale {aa:HH:mm})"
                : d.DelayMinutes is > 0 ? $"{a:HH:mm} → {a.AddMinutes(d.DelayMinutes.Value):HH:mm}"
                : a.ToString("HH:mm"))
            : "—";
        DelayText = Palette.DelayText(d.DelayMinutes, d.IsCancelled);
        DelayColor = Palette.Delay(d.DelayMinutes, d.IsCancelled, s.DelayThresholdMinutes);
        PlatformText = string.IsNullOrWhiteSpace(d.Platform)
            ? "Binario non disponibile"
            : $"Binario {d.Platform}{(d.PlatformConfirmed ? "" : " (previsto)")}";

        ShowCrowding = s.ShowCrowding;
        if (d.CrowdingPercent is int p)
        {
            CrowdingText = $"Affollamento {p}%";
            CrowdingColor = Palette.Crowding(p);
            CrowdingProgress = p / 100.0;
            CrowdingSourceText = $"Fonte: {d.CrowdingSource}";
        }
        else
        {
            CrowdingText = "Affollamento non disponibile";
            CrowdingColor = Palette.Unknown;
            CrowdingProgress = 0;
            CrowdingSourceText = string.IsNullOrWhiteSpace(d.CrowdingSource)
                ? "Nessuna fonte indicata"
                : $"Fonte \"{d.CrowdingSource}\" non tra quelle affidabili";
        }

        Stops.Clear();
        foreach (var stop in d.Stops)
            Stops.Add(new StopItemViewModel(stop, _leg, s.DelayThresholdMinutes));

        LastUpdate = $"Aggiornato alle {ViaggiaTrenoDate.NowRome():HH:mm:ss}";
    }
}

public sealed class StopItemViewModel
{
    public StopItemViewModel(StopInfo stop, RouteLeg? leg, int delayThreshold)
    {
        Name = string.IsNullOrWhiteSpace(stop.StationName) ? "(stazione sconosciuta)" : DepartureItemViewModel.Capitalize(stop.StationName);
        ScheduledText = stop.Scheduled?.ToString("HH:mm") ?? "";
        ActualText = stop.Actual?.ToString("HH:mm") ?? "";
        PlatformText = string.IsNullOrWhiteSpace(stop.Platform) ? "" : $"bin. {stop.Platform}";
        IsCancelled = stop.IsCancelled;
        HasPassed = stop.HasPassed;
        StatusText = stop.IsCancelled ? "soppressa"
            : stop.DelayMinutes is null ? ""
            : Palette.DelayText(stop.DelayMinutes, false);
        StatusColor = Palette.Delay(stop.DelayMinutes, stop.IsCancelled, delayThreshold);
        IsOnLeg = leg is not null
                  && (StationNameMatcher.Matches(stop.StationName, leg.FromName)
                      || StationNameMatcher.Matches(stop.StationName, leg.ToName));
        DotColor = IsCancelled ? Palette.Late : HasPassed ? Palette.Unknown : Color.FromArgb("#0B5D3B");
        NameOpacity = HasPassed ? 0.55 : 1.0;
        IsFallback = stop.FromFallback;
    }

    public string Name { get; }
    public string ScheduledText { get; }
    public string ActualText { get; }
    public string PlatformText { get; }
    public bool IsCancelled { get; }
    public bool HasPassed { get; }
    public string StatusText { get; }
    public Color StatusColor { get; }
    public bool IsOnLeg { get; }
    public Color DotColor { get; }
    public double NameOpacity { get; }
    public bool IsFallback { get; }
    public FontAttributes NameAttributes => IsOnLeg ? FontAttributes.Bold : FontAttributes.None;
}
