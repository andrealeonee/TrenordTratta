using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrenordTratta.Core.Models;
using TrenordTratta.Core.Settings;

namespace TrenordTratta.Mobile.ViewModels;

/// <summary>Una riga della lista partenze. I comandi arrivano dal padre per evitare binding a RelativeSource.</summary>
public partial class DepartureItemViewModel : ObservableObject
{
    public DepartureItemViewModel(Departure departure, AppSettings settings, bool isTaken,
        Func<DepartureItemViewModel, Task> open, Func<DepartureItemViewModel, Task> toggleTaken)
    {
        Departure = departure;
        _isTaken = isTaken;

        var d = departure;
        TimeText = d.ScheduledDeparture.ToString("HH:mm");
        ExpectedText = d.DelayMinutes is > 0 && !d.IsCancelled ? d.ExpectedDeparture.ToString("HH:mm") : "";
        DelayText = Palette.DelayText(d.DelayMinutes, d.IsCancelled);
        DelayColor = Palette.Delay(d.DelayMinutes, d.IsCancelled, settings.DelayThresholdMinutes);

        TrainText = $"{d.Category} {d.TrainNumber}".Trim();
        TerminusText = string.IsNullOrWhiteSpace(d.Terminus) ? "" : $"per {Capitalize(d.Terminus)}";
        ArrivalText = d.ScheduledArrival is DateTime arr
            ? $"arr. {(d.DelayMinutes is > 0 ? arr.AddMinutes(d.DelayMinutes.Value) : arr):HH:mm}"
            : "";
        PlatformText = string.IsNullOrWhiteSpace(d.Platform) ? "" : $"Bin. {d.Platform}";
        PlatformConfirmed = d.PlatformConfirmed;

        ShowCrowding = settings.ShowCrowding && d.CrowdingPercent is not null;
        CrowdingColor = d.CrowdingPercent is int p ? Palette.Crowding(p) : Palette.Unknown;
        CrowdingText = d.CrowdingPercent is int pc ? $"{pc}%" : "";

        OpenCommand = new AsyncRelayCommand(() => open(this));
        ToggleTakenCommand = new AsyncRelayCommand(() => toggleTaken(this));
    }

    public Departure Departure { get; }

    public string TimeText { get; }
    public string ExpectedText { get; }
    public bool HasExpected => ExpectedText.Length > 0;
    public string DelayText { get; }
    public Color DelayColor { get; }
    public string TrainText { get; }
    public string TerminusText { get; }
    public string ArrivalText { get; }
    public string PlatformText { get; }
    public bool PlatformConfirmed { get; }
    public bool ShowCrowding { get; }
    public Color CrowdingColor { get; }
    public string CrowdingText { get; }
    public bool IsCancelled => Departure.IsCancelled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TakenSwipeText))]
    private bool _isTaken;

    public string TakenSwipeText => IsTaken ? "Annulla" : "Lo prendo";

    public ICommand OpenCommand { get; }
    public ICommand ToggleTakenCommand { get; }

    internal static string Capitalize(string s) =>
        string.Join(' ', s.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}
