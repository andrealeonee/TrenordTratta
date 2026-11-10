using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TrenordTratta.Core.Data;
using TrenordTratta.Core.Models;
using TrenordTratta.Core.Services;
using TrenordTratta.Core.Settings;

namespace TrenordTratta.Mobile.ViewModels;

public partial class DeparturesViewModel(
    IDepartureService departures,
    IRouteRepository routes,
    IRouteService routeService,
    ITripRepository trips,
    ISettingsService settings,
    DirectionResolver direction,
    ILogger<DeparturesViewModel> logger) : ObservableObject
{
    private CancellationTokenSource? _refreshCts;
    private bool _loadingRoutes;

    public ObservableCollection<RouteDefinition> Routes { get; } = [];
    public ObservableCollection<DepartureItemViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMultipleRoutes))]
    private RouteDefinition? _selectedRoute;

    [ObservableProperty] private string _legLabel = "";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private bool _isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarning))]
    private string _warningText = "";

    [ObservableProperty] private string _emptyText = "Nessun treno nelle prossime ore";

    public bool HasMultipleRoutes => Routes.Count > 1;
    public bool HasError => ErrorMessage.Length > 0;
    public bool HasWarning => WarningText.Length > 0;

    public RouteLeg? CurrentLeg { get; private set; }

    public int AutoRefreshSeconds => settings.Current.AutoRefreshSeconds;

    private DateTime _lastRefresh = DateTime.MinValue;
    private AppSettings? _settingsUsed;
    private static readonly TimeSpan MinAppearRefresh = TimeSpan.FromSeconds(20);

    public async Task OnAppearingAsync()
    {
        await LoadRoutesAsync();

        // Tornando dal dettaglio o da un'altra tab non serve rifare subito le chiamate
        // (se le impostazioni sono cambiate il record è un'istanza nuova → refresh)
        if (DateTime.UtcNow - _lastRefresh < MinAppearRefresh && Items.Count > 0
            && ReferenceEquals(_settingsUsed, settings.Current))
            await SyncTakenAsync();
        else
            await RefreshAsync();
    }

    public async Task LoadRoutesAsync()
    {
        _loadingRoutes = true;
        try
        {
            var all = await routes.GetAllAsync();
            var currentId = SelectedRoute?.Id ?? settings.Current.SelectedRouteId;

            Routes.Clear();
            foreach (var r in all) Routes.Add(r);

            SelectedRoute = all.FirstOrDefault(r => r.Id == currentId) ?? all.FirstOrDefault();
            OnPropertyChanged(nameof(HasMultipleRoutes));
        }
        finally
        {
            _loadingRoutes = false;
        }
    }

    partial void OnSelectedRouteChanged(RouteDefinition? value)
    {
        if (_loadingRoutes || value is null) return;
        settings.Save(settings.Current with { SelectedRouteId = value.Id });
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (SelectedRoute is null) return;

        _refreshCts?.Cancel();
        var cts = _refreshCts = new CancellationTokenSource();
        IsRefreshing = true;

        try
        {
            var s = settings.Current;
            var now = ViaggiaTrenoDate.NowRome();
            var route = await routeService.EnsureCodesAsync(SelectedRoute, cts.Token);

            var dir = direction.Resolve(now, s.DirectionSwitchHour);
            var leg = route.GetLeg(dir);
            CurrentLeg = leg;
            LegLabel = leg.Label;

            var board = await departures.GetDeparturesAsync(leg, s, cts.Token);
            if (cts.IsCancellationRequested) return;

            await trips.UpdateLatestAsync(board.Departures, now);
            var takenToday = (await trips.GetForDayAsync(now)).Select(t => t.TrainNumber).ToHashSet();

            Items.Clear();
            foreach (var d in board.Departures)
                Items.Add(new DepartureItemViewModel(d, s, takenToday.Contains(d.TrainNumber), OpenDetailAsync, ToggleTakenAsync));

            _lastRefresh = DateTime.UtcNow;
            _settingsUsed = s;
            ErrorMessage = "";
            WarningText = string.Join("\n", board.Warnings);
            Subtitle = $"{(direction.IsManual ? "Direzione manuale" : "Direzione automatica")} · agg. {board.FetchedAt:HH:mm:ss}";
            EmptyText = "Nessun treno nelle prossime ore";
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // sostituito da un refresh più recente
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Refresh partenze fallito");
            ErrorMessage = ex is InvalidOperationException
                ? ex.Message
                : $"Impossibile aggiornare le partenze.\nDettaglio: {Describe(ex)}";
            EmptyText = "";
        }
        finally
        {
            if (ReferenceEquals(_refreshCts, cts)) IsRefreshing = false;
        }
    }

    /// <summary>Messaggio diagnostico breve: tipo + messaggio, risalendo alle inner exception.</summary>
    internal static string Describe(Exception ex)
    {
        var parts = new List<string>();
        for (var e = ex; e is not null && parts.Count < 3; e = e.InnerException)
            parts.Add($"{e.GetType().Name}: {e.Message}");
        return string.Join(" → ", parts);
    }

    [RelayCommand]
    private async Task ToggleDirectionAsync()
    {
        direction.Toggle(ViaggiaTrenoDate.NowRome(), settings.Current.DirectionSwitchHour);
        Items.Clear();
        await RefreshAsync();
    }

    private async Task OpenDetailAsync(DepartureItemViewModel item)
    {
        if (CurrentLeg is null || SelectedRoute is null) return;

        await Shell.Current.GoToAsync(AppShell.TrainRoute, new ShellNavigationQueryParameters
        {
            ["departure"] = item.Departure,
            ["leg"] = CurrentLeg,
            ["route"] = SelectedRoute,
            ["taken"] = item.IsTaken,
        });
    }

    private async Task ToggleTakenAsync(DepartureItemViewModel item)
    {
        if (CurrentLeg is null || SelectedRoute is null) return;

        try
        {
            if (item.IsTaken)
                await trips.UnmarkAsync(item.Departure.TrainNumber, item.Departure.TrainDate);
            else
                await trips.MarkTakenAsync(item.Departure, SelectedRoute, CurrentLeg, ViaggiaTrenoDate.NowRome());

            item.IsTaken = !item.IsTaken;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Salvataggio viaggio fallito");
            ErrorMessage = "Non sono riuscito a salvare il viaggio.";
        }
    }

    /// <summary>Allinea lo stato "preso" quando si torna dal dettaglio.</summary>
    public async Task SyncTakenAsync()
    {
        var taken = (await trips.GetForDayAsync(ViaggiaTrenoDate.NowRome())).Select(t => t.TrainNumber).ToHashSet();
        foreach (var item in Items) item.IsTaken = taken.Contains(item.Departure.TrainNumber);
    }
}
