using TrenordTratta.Core.Settings;

namespace TrenordTratta.Mobile.Services;

/// <summary>Impostazioni su Preferences (SharedPreferences su Android).</summary>
public sealed class MauiSettingsService : ISettingsService
{
    private readonly IPreferences _prefs = Preferences.Default;
    private AppSettings? _cache;

    public event EventHandler<AppSettings>? Changed;

    public AppSettings Current => _cache ??= Load();

    public void Save(AppSettings settings)
    {
        _prefs.Set(nameof(AppSettings.ShowCrowding), settings.ShowCrowding);
        _prefs.Set(nameof(AppSettings.DelayThresholdMinutes), settings.DelayThresholdMinutes);
        _prefs.Set(nameof(AppSettings.AutoRefreshSeconds), settings.AutoRefreshSeconds);
        _prefs.Set(nameof(AppSettings.DirectionSwitchHour), settings.DirectionSwitchHour);
        _prefs.Set(nameof(AppSettings.LookAheadMinutes), settings.LookAheadMinutes);
        _prefs.Set(nameof(AppSettings.MaxTrains), settings.MaxTrains);
        _prefs.Set(nameof(AppSettings.TrustedCrowdingSources), settings.TrustedCrowdingSources);
        _prefs.Set(nameof(AppSettings.SelectedRouteId), settings.SelectedRouteId);

        _cache = settings;
        Changed?.Invoke(this, settings);
    }

    private AppSettings Load()
    {
        var d = new AppSettings();
        return new AppSettings
        {
            ShowCrowding = _prefs.Get(nameof(AppSettings.ShowCrowding), d.ShowCrowding),
            DelayThresholdMinutes = _prefs.Get(nameof(AppSettings.DelayThresholdMinutes), d.DelayThresholdMinutes),
            AutoRefreshSeconds = _prefs.Get(nameof(AppSettings.AutoRefreshSeconds), d.AutoRefreshSeconds),
            DirectionSwitchHour = _prefs.Get(nameof(AppSettings.DirectionSwitchHour), d.DirectionSwitchHour),
            LookAheadMinutes = _prefs.Get(nameof(AppSettings.LookAheadMinutes), d.LookAheadMinutes),
            MaxTrains = _prefs.Get(nameof(AppSettings.MaxTrains), d.MaxTrains),
            TrustedCrowdingSources = _prefs.Get(nameof(AppSettings.TrustedCrowdingSources), d.TrustedCrowdingSources),
            SelectedRouteId = _prefs.Get(nameof(AppSettings.SelectedRouteId), d.SelectedRouteId),
        };
    }
}
