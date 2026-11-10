using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TrenordTratta.Core.Api.ViaggiaTreno;
using TrenordTratta.Core.Data;
using TrenordTratta.Core.Models;
using TrenordTratta.Core.Services;

namespace TrenordTratta.Mobile.ViewModels;

/// <summary>Creazione/modifica tratta: le stazioni si scelgono dall'elenco ViaggiaTreno, così il codice è sempre valido.</summary>
public partial class RouteEditViewModel(
    IRouteRepository routes,
    IRouteService routeService,
    ILogger<RouteEditViewModel> logger) : ObservableObject, IQueryAttributable
{
    private RouteDefinition _route = new();
    private CancellationTokenSource? _searchCts;
    private bool _suppress;

    public ObservableCollection<VtStation> Results { get; } = [];

    [ObservableProperty] private string _pageTitle = "Nuova tratta";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _stationAText = "";
    [ObservableProperty] private string _stationBText = "";
    [ObservableProperty] private string _stationACode = "";
    [ObservableProperty] private string _stationBCode = "";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _canDelete;

    /// <summary>"A" o "B": quale campo sta cercando.</summary>
    [ObservableProperty] private string _searchTarget = "";

    public bool HasResults => Results.Count > 0;

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var id = query.TryGetValue("routeId", out var v) && v is int i ? i : 0;
        try
        {
            _route = id == 0 ? new RouteDefinition() : await routes.GetAsync(id) ?? new RouteDefinition();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Caricamento tratta fallito");
            _route = new RouteDefinition();
        }

        _suppress = true;
        PageTitle = _route.Id == 0 ? "Nuova tratta" : "Modifica tratta";
        CanDelete = _route.Id != 0;
        Name = _route.Name;
        StationAText = _route.StationAName;
        StationACode = _route.StationACode;
        StationBText = _route.StationBName;
        StationBCode = _route.StationBCode;
        _suppress = false;
    }

    // Digitando si invalida il codice: va riscelta la stazione dall'elenco
    partial void OnStationATextChanged(string value)
    {
        if (_suppress) return;
        StationACode = "";
        _ = SearchAsync("A", value);
    }

    partial void OnStationBTextChanged(string value)
    {
        if (_suppress) return;
        StationBCode = "";
        _ = SearchAsync("B", value);
    }

    private async Task SearchAsync(string target, string text)
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        SearchTarget = target;

        try
        {
            await Task.Delay(350, cts.Token); // debounce
            IsSearching = true;
            var found = await routeService.SearchStationsAsync(text, cts.Token);
            if (cts.IsCancellationRequested) return;

            Results.Clear();
            foreach (var s in found.Take(15)) Results.Add(s);
            OnPropertyChanged(nameof(HasResults));
            ErrorMessage = "";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ricerca stazioni fallita");
            ErrorMessage = "Ricerca stazioni non riuscita.";
        }
        finally
        {
            if (ReferenceEquals(_searchCts, cts)) IsSearching = false;
        }
    }

    [RelayCommand]
    private void Pick(VtStation station)
    {
        _searchCts?.Cancel();
        var name = DepartureItemViewModel.Capitalize(station.Name);

        _suppress = true;
        if (SearchTarget == "A")
        {
            StationAText = name;
            StationACode = station.Code;
        }
        else if (SearchTarget == "B")
        {
            StationBText = name;
            StationBCode = station.Code;
        }
        _suppress = false;

        SearchTarget = "";
        Results.Clear();
        OnPropertyChanged(nameof(HasResults));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(StationACode) || string.IsNullOrWhiteSpace(StationBCode))
        {
            ErrorMessage = "Scegli entrambe le stazioni dall'elenco dei risultati.";
            return;
        }
        if (StationACode == StationBCode)
        {
            ErrorMessage = "Le due stazioni devono essere diverse.";
            return;
        }

        _route.Name = string.IsNullOrWhiteSpace(Name) ? $"{StationAText} ↔ {StationBText}" : Name.Trim();
        _route.StationAName = StationAText.Trim();
        _route.StationACode = StationACode;
        _route.StationBName = StationBText.Trim();
        _route.StationBCode = StationBCode;

        await routes.SaveAsync(_route);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_route.Id == 0) return;
        var ok = await Shell.Current.DisplayAlertAsync("Elimina tratta", $"Eliminare \"{_route.Name}\"?", "Elimina", "Annulla");
        if (!ok) return;

        try
        {
            await routes.DeleteAsync(_route.Id);
            await Shell.Current.GoToAsync("..");
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
