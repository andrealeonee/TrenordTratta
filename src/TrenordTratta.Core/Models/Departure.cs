namespace TrenordTratta.Core.Models;

/// <summary>
/// Una partenza già "fusa": dati ViaggiaTreno arricchiti con quelli Trenord.
/// Chiave di merge: numero treno + codice origine + giorno.
/// </summary>
public sealed class Departure
{
    public required string TrainNumber { get; init; }
    public string Category { get; init; } = "";
    public required string OriginCode { get; init; }
    public required DateTime TrainDate { get; init; }

    /// <summary>Capolinea del treno (non necessariamente la destinazione della tratta).</summary>
    public string Terminus { get; init; } = "";

    public bool IsTrenord { get; init; }

    public required DateTime ScheduledDeparture { get; init; }
    public DateTime? ActualDeparture { get; set; }

    /// <summary>Ritardo in minuti; null se sconosciuto.</summary>
    public int? DelayMinutes { get; set; }

    public string? Platform { get; set; }
    public bool PlatformConfirmed { get; set; }

    public DateTime? ScheduledArrival { get; set; }
    public DateTime? ActualArrival { get; set; }

    public bool IsCancelled { get; set; }
    public bool HasDeparted { get; set; }

    /// <summary>Affollamento in %; valorizzato solo se la fonte è considerata affidabile.</summary>
    public int? CrowdingPercent { get; set; }
    public string? CrowdingSource { get; set; }

    /// <summary>Fermate del treno, se recuperate (servono al dettaglio).</summary>
    public IReadOnlyList<StopInfo> Stops { get; set; } = [];

    /// <summary>true se la fermata a destinazione è stata verificata sulle fermate o sul capolinea.</summary>
    public bool DestinationVerified { get; set; }

    /// <summary>Riga ViaggiaTreno di partenza: serve per ricaricare il treno dal dettaglio.</summary>
    internal Api.ViaggiaTreno.VtDeparture? VtSource { get; init; }

    public DepartureKey Key => new(TrainNumber, OriginCode, TrainDate.Date);

    public DateTime ExpectedDeparture => ActualDeparture
        ?? (DelayMinutes is int d ? ScheduledDeparture.AddMinutes(d) : ScheduledDeparture);
}

public readonly record struct DepartureKey(string TrainNumber, string OriginCode, DateTime Day);

public sealed class StopInfo
{
    public required string StationName { get; init; }
    public string? StationCode { get; init; }

    /// <summary>O origine, F fermata, D destinazione.</summary>
    public StopType Type { get; init; }

    public DateTime? ScheduledArrival { get; init; }
    public DateTime? ScheduledDeparture { get; init; }
    public DateTime? ActualArrival { get; init; }
    public DateTime? ActualDeparture { get; init; }

    public string? Platform { get; init; }
    public bool IsCancelled { get; init; }

    /// <summary>true se la stazione non arrivava da Trenord ed è stata presa da ViaggiaTreno.</summary>
    public bool FromFallback { get; init; }

    public DateTime? Scheduled => ScheduledDeparture ?? ScheduledArrival;
    public DateTime? Actual => ActualDeparture ?? ActualArrival;
    public bool HasPassed => Actual is not null;

    public int? DelayMinutes => Scheduled is DateTime s && Actual is DateTime a
        ? (int)Math.Round((a - s).TotalMinutes)
        : null;
}

public enum StopType
{
    Stop,
    Origin,
    Destination
}
