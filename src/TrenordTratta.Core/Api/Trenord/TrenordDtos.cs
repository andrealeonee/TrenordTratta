using System.Text.Json.Serialization;

namespace TrenordTratta.Core.Api.Trenord;

// Struttura: [ { journey_list: [ { train: {...}, pass_list: [...] } ] } ]

public sealed class TrenordResult
{
    [JsonPropertyName("journey_list")]
    public List<TrenordJourney>? JourneyList { get; set; }
}

public sealed class TrenordJourney
{
    [JsonPropertyName("train")]
    public TrenordTrain? Train { get; set; }

    [JsonPropertyName("pass_list")]
    public List<TrenordPass>? PassList { get; set; }
}

public sealed class TrenordTrain
{
    /// <summary>yyyyMMdd</summary>
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    /// <summary>Valorizzato se il treno è partito.</summary>
    [JsonPropertyName("actual_time")]
    public string? ActualTime { get; set; }

    [JsonPropertyName("crowding")]
    public TrenordCrowding? Crowding { get; set; }
}

public sealed class TrenordCrowding
{
    [JsonPropertyName("percentage")]
    public int? Percentage { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }
}

public sealed class TrenordPass
{
    /// <summary>Può mancare: in quel caso si ripiega su ViaggiaTreno.</summary>
    [JsonPropertyName("station")]
    public TrenordStation? Station { get; set; }

    /// <summary>O origine, F fermata, D destinazione.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("cancelled")]
    public bool Cancelled { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    /// <summary>HH:mm:ss</summary>
    [JsonPropertyName("arr_time")]
    public string? ArrTime { get; set; }

    /// <summary>HH:mm:ss</summary>
    [JsonPropertyName("dep_time")]
    public string? DepTime { get; set; }

    [JsonPropertyName("actual_data")]
    public TrenordActualData? ActualData { get; set; }
}

public sealed class TrenordStation
{
    [JsonPropertyName("station_id")]
    public string? StationId { get; set; }

    [JsonPropertyName("station_ori_name")]
    public string? StationOriName { get; set; }
}

public sealed class TrenordActualData
{
    [JsonPropertyName("actual_station_mir")]
    public string? ActualStationMir { get; set; }

    [JsonPropertyName("actual_type")]
    public string? ActualType { get; set; }

    [JsonPropertyName("arr_actual_time")]
    public string? ArrActualTime { get; set; }

    [JsonPropertyName("dep_actual_time")]
    public string? DepActualTime { get; set; }
}
