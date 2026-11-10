using System.Text.Json.Serialization;

namespace TrenordTratta.Core.Api.ViaggiaTreno;

/// <summary>Elemento di /partenze/{codStazione}/{data}. Timestamp in millisecondi Unix.</summary>
public sealed class VtDeparture
{
    [JsonPropertyName("numeroTreno")]
    public string? NumeroTreno { get; set; }

    [JsonPropertyName("categoria")]
    public string? Categoria { get; set; }

    [JsonPropertyName("categoriaDescrizione")]
    public string? CategoriaDescrizione { get; set; }

    [JsonPropertyName("codOrigine")]
    public string? CodOrigine { get; set; }

    [JsonPropertyName("destinazione")]
    public string? Destinazione { get; set; }

    [JsonPropertyName("orarioPartenza")]
    public long? OrarioPartenza { get; set; }

    [JsonPropertyName("dataPartenzaTreno")]
    public long? DataPartenzaTreno { get; set; }

    [JsonPropertyName("ritardo")]
    public int? Ritardo { get; set; }

    [JsonPropertyName("binarioProgrammatoPartenzaDescrizione")]
    public string? BinarioProgrammato { get; set; }

    [JsonPropertyName("binarioEffettivoPartenzaDescrizione")]
    public string? BinarioEffettivo { get; set; }

    [JsonPropertyName("inStazione")]
    public bool InStazione { get; set; }

    [JsonPropertyName("nonPartito")]
    public bool NonPartito { get; set; }

    /// <summary>63 = Trenord. Nel JSON ViaggiaTreno si chiama codiceCliente.</summary>
    [JsonPropertyName("codiceCliente")]
    public int? CodiceCliente { get; set; }

    /// <summary>Alcuni wrapper lo espongono come client_code: lo leggiamo comunque.</summary>
    [JsonPropertyName("client_code")]
    public int? ClientCode { get; set; }

    /// <summary>Provvedimento: 1 = treno soppresso (valori diversi da 0 indicano anomalie).</summary>
    [JsonPropertyName("provvedimento")]
    public int? Provvedimento { get; set; }

    [JsonIgnore]
    public int? ClientCodeResolved => CodiceCliente ?? ClientCode;
}

/// <summary>Risposta di /andamentoTreno/{codOrigine}/{numero}/{dataPartenzaMs}, usata come fallback per le fermate.</summary>
public sealed class VtTrainStatus
{
    [JsonPropertyName("fermate")]
    public List<VtStop>? Fermate { get; set; }
}

public sealed class VtStop
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("stazione")]
    public string? Stazione { get; set; }

    /// <summary>P partenza, F fermata, A arrivo.</summary>
    [JsonPropertyName("tipoFermata")]
    public string? TipoFermata { get; set; }

    [JsonPropertyName("arrivo_teorico")]
    public long? ArrivoTeorico { get; set; }

    [JsonPropertyName("partenza_teorica")]
    public long? PartenzaTeorica { get; set; }

    [JsonPropertyName("arrivoReale")]
    public long? ArrivoReale { get; set; }

    [JsonPropertyName("partenzaReale")]
    public long? PartenzaReale { get; set; }

    [JsonPropertyName("binarioEffettivoPartenzaDescrizione")]
    public string? BinarioEffettivo { get; set; }

    [JsonPropertyName("binarioProgrammatoPartenzaDescrizione")]
    public string? BinarioProgrammato { get; set; }

    /// <summary>3 = fermata soppressa.</summary>
    [JsonPropertyName("actualFermataType")]
    public int? ActualFermataType { get; set; }
}

public sealed record VtStation(string Name, string Code);
