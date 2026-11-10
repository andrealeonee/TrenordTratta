using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrenordTratta.Core.Data;
using TrenordTratta.Core.Models;
using TrenordTratta.Core.Settings;

namespace TrenordTratta.Mobile.ViewModels;

/// <summary>Storico in lista (no calendario), raggruppato per giorno, più recente in alto.</summary>
public partial class HistoryViewModel(ITripRepository trips, ISettingsService settings) : ObservableObject
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    public ObservableCollection<TripGroup> Groups { get; } = [];

    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private bool _isEmpty;

    [RelayCommand]
    public async Task LoadAsync()
    {
        var all = await trips.GetAllAsync();
        var threshold = settings.Current.DelayThresholdMinutes;

        Groups.Clear();
        foreach (var g in all.GroupBy(t => t.TrainDate.Date).OrderByDescending(g => g.Key))
        {
            var title = Capitalize(g.Key.ToString("dddd d MMMM yyyy", It));
            Groups.Add(new TripGroup(title, g.OrderByDescending(t => t.ScheduledDeparture)
                .Select(t => new TripItemViewModel(t, threshold, DeleteAsync))));
        }

        IsEmpty = all.Count == 0;
        var withDelay = all.Where(t => t.DelayMinutes is not null && !t.WasCancelled).ToList();
        var late = withDelay.Count(t => t.DelayMinutes > threshold);
        Summary = all.Count == 0
            ? ""
            : $"{all.Count} viaggi · {late} in ritardo"
              + (withDelay.Count > 0 ? $" · ritardo medio {withDelay.Average(t => Math.Max(0, t.DelayMinutes!.Value)):0.#}'" : "");
    }

    private async Task DeleteAsync(TripItemViewModel item)
    {
        var confirm = await Shell.Current.DisplayAlertAsync("Elimina viaggio",
            $"Eliminare {item.TrainText} del {item.Trip.TrainDate:dd/MM}?", "Elimina", "Annulla");
        if (!confirm) return;

        await trips.DeleteAsync(item.Trip.Id);
        await LoadAsync();
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0], It) + s[1..];
}

public sealed class TripGroup(string title, IEnumerable<TripItemViewModel> items) : List<TripItemViewModel>(items)
{
    public string Title { get; } = title;
}

public sealed class TripItemViewModel
{
    public TripItemViewModel(TripRecord trip, int threshold, Func<TripItemViewModel, Task> delete)
    {
        Trip = trip;
        TimeText = trip.ScheduledDeparture.ToString("HH:mm")
                   + (trip.ScheduledArrival is DateTime a ? $" – {a:HH:mm}" : "");
        TrainText = $"{trip.Category} {trip.TrainNumber}".Trim();
        LegText = $"{trip.FromName} → {trip.ToName}";
        DelayText = Palette.DelayText(trip.DelayMinutes, trip.WasCancelled);
        DelayColor = Palette.Delay(trip.DelayMinutes, trip.WasCancelled, threshold);
        CrowdingText = trip.CrowdingPercent is int p ? $"{p}%" : "";
        CrowdingColor = trip.CrowdingPercent is int pc ? Palette.Crowding(pc) : Palette.Unknown;
        HasCrowding = trip.CrowdingPercent is not null;
        DeleteCommand = new AsyncRelayCommand(() => delete(this));
    }

    public TripRecord Trip { get; }
    public string TimeText { get; }
    public string TrainText { get; }
    public string LegText { get; }
    public string DelayText { get; }
    public Color DelayColor { get; }
    public string CrowdingText { get; }
    public Color CrowdingColor { get; }
    public bool HasCrowding { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
}
