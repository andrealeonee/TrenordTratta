using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrenordTratta.Core.Data;
using TrenordTratta.Core.Models;
using TrenordTratta.Core.Settings;

namespace TrenordTratta.Mobile.ViewModels;

/// <summary>Le modifiche si salvano subito. Gli Stepper lavorano in double, da qui le conversioni.</summary>
public partial class SettingsViewModel : ObservableObject
{
    private static readonly int[] RefreshValues = [0, 30, 60, 120, 300];

    private readonly ISettingsService _settings;
    private readonly IRouteRepository _routes;
    private bool _loading;

    public SettingsViewModel(ISettingsService settings, IRouteRepository routes)
    {
        _settings = settings;
        _routes = routes;
        Load(settings.Current);
    }

    public ObservableCollection<RouteDefinition> Routes { get; } = [];

    public IReadOnlyList<string> RefreshLabels { get; } = ["Disattivato", "30 secondi", "1 minuto", "2 minuti", "5 minuti"];

    [ObservableProperty] private bool _showCrowding;
    [ObservableProperty] private double _delayThreshold;
    [ObservableProperty] private int _refreshIndex;
    [ObservableProperty] private double _switchHour;
    [ObservableProperty] private double _lookAheadMinutes;
    [ObservableProperty] private double _maxTrains;
    [ObservableProperty] private string _trustedSources = "";

    public string DelayThresholdText => DelayThreshold < 1
        ? "Rosso con qualsiasi ritardo"
        : $"Verde fino a {DelayThreshold:0}', rosso oltre";
    public string SwitchHourText => $"Andata prima delle {SwitchHour:00}:00, ritorno dopo";
    public string LookAheadText => $"Prossime {LookAheadMinutes / 60:0} ore";
    public string MaxTrainsText => $"Al massimo {MaxTrains:0} treni";

    private void Load(AppSettings s)
    {
        _loading = true;
        ShowCrowding = s.ShowCrowding;
        DelayThreshold = s.DelayThresholdMinutes;
        RefreshIndex = Math.Max(0, Array.IndexOf(RefreshValues, s.AutoRefreshSeconds));
        SwitchHour = s.DirectionSwitchHour;
        LookAheadMinutes = s.LookAheadMinutes;
        MaxTrains = s.MaxTrains;
        TrustedSources = s.TrustedCrowdingSources;
        _loading = false;
    }

    partial void OnShowCrowdingChanged(bool value) => Save();
    partial void OnDelayThresholdChanged(double value) { OnPropertyChanged(nameof(DelayThresholdText)); Save(); }
    partial void OnRefreshIndexChanged(int value) => Save();
    partial void OnSwitchHourChanged(double value) { OnPropertyChanged(nameof(SwitchHourText)); Save(); }
    partial void OnLookAheadMinutesChanged(double value) { OnPropertyChanged(nameof(LookAheadText)); Save(); }
    partial void OnMaxTrainsChanged(double value) { OnPropertyChanged(nameof(MaxTrainsText)); Save(); }
    partial void OnTrustedSourcesChanged(string value) => Save();

    private void Save()
    {
        if (_loading) return;
        _settings.Save(_settings.Current with
        {
            ShowCrowding = ShowCrowding,
            DelayThresholdMinutes = (int)Math.Round(DelayThreshold),
            AutoRefreshSeconds = RefreshValues[Math.Clamp(RefreshIndex, 0, RefreshValues.Length - 1)],
            DirectionSwitchHour = (int)Math.Round(SwitchHour),
            LookAheadMinutes = (int)Math.Round(LookAheadMinutes),
            MaxTrains = (int)Math.Round(MaxTrains),
            TrustedCrowdingSources = TrustedSources.Trim(),
        });
    }

    public async Task LoadRoutesAsync()
    {
        Routes.Clear();
        foreach (var r in await _routes.GetAllAsync()) Routes.Add(r);
    }

    [RelayCommand]
    private Task AddRouteAsync() =>
        Shell.Current.GoToAsync(AppShell.RouteEditRoute, new ShellNavigationQueryParameters { ["routeId"] = 0 });

    [RelayCommand]
    private Task EditRouteAsync(RouteDefinition route) =>
        Shell.Current.GoToAsync(AppShell.RouteEditRoute, new ShellNavigationQueryParameters { ["routeId"] = route.Id });
}
