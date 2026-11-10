using SQLite;

namespace TrenordTratta.Core.Models;

/// <summary>Un treno segnato come "preso". Alimenta lo storico.</summary>
[Table("trips")]
public class TripRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int RouteId { get; set; }

    [NotNull]
    public string FromName { get; set; } = "";

    [NotNull]
    public string ToName { get; set; } = "";

    [NotNull, Indexed(Name = "IX_trip_train_day", Order = 1, Unique = true)]
    public string TrainNumber { get; set; } = "";

    /// <summary>Giorno del treno (solo data, ora locale).</summary>
    [Indexed(Name = "IX_trip_train_day", Order = 2, Unique = true)]
    public DateTime TrainDate { get; set; }

    public string Category { get; set; } = "";

    public DateTime ScheduledDeparture { get; set; }

    public DateTime? ScheduledArrival { get; set; }

    /// <summary>Ultimo ritardo rilevato (minuti).</summary>
    public int? DelayMinutes { get; set; }

    /// <summary>Ultimo affollamento rilevato (%).</summary>
    public int? CrowdingPercent { get; set; }

    public bool WasCancelled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastUpdatedAt { get; set; }
}
