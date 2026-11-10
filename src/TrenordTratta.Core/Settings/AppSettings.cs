namespace TrenordTratta.Core.Settings;

public sealed record AppSettings
{
    /// <summary>Mostra il pallino di affollamento accanto all'orario.</summary>
    public bool ShowCrowding { get; init; } = true;

    /// <summary>Fino a questo ritardo (minuti) il treno è "in orario" (verde), oltre è rosso.</summary>
    public int DelayThresholdMinutes { get; init; } = 2;

    /// <summary>Intervallo di auto-refresh in secondi; 0 = disattivato.</summary>
    public int AutoRefreshSeconds { get; init; } = 60;

    /// <summary>Prima di quest'ora la direzione automatica è A→B, dopo B→A.</summary>
    public int DirectionSwitchHour { get; init; } = 13;

    /// <summary>Quanti minuti avanti cercare le partenze.</summary>
    public int LookAheadMinutes { get; init; } = 120;

    /// <summary>Numero massimo di treni mostrati.</summary>
    public int MaxTrains { get; init; } = 10;

    /// <summary>
    /// Fonti di affollamento considerate affidabili, separate da virgola (confronto case-insensitive).
    /// Vuoto = qualsiasi fonte non vuota. Una percentuale senza fonte non viene mai mostrata.
    /// </summary>
    public string TrustedCrowdingSources { get; init; } = "";

    public int SelectedRouteId { get; init; }

    public bool IsCrowdingSourceTrusted(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        if (string.IsNullOrWhiteSpace(TrustedCrowdingSources)) return true;
        return TrustedCrowdingSources
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(s => string.Equals(s, source.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}

public interface ISettingsService
{
    AppSettings Current { get; }
    void Save(AppSettings settings);
    event EventHandler<AppSettings>? Changed;
}
