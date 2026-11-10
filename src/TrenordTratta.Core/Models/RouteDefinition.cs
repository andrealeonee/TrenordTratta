using SQLite;

namespace TrenordTratta.Core.Models;

/// <summary>
/// Una tratta tra due stazioni (A ↔ B). Le direzioni si ottengono invertendo A e B.
/// I codici sono quelli di ViaggiaTreno (es. "S01700"), risolti con autocompletaStazione.
/// </summary>
[Table("routes")]
public class RouteDefinition
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string Name { get; set; } = "";

    [NotNull]
    public string StationAName { get; set; } = "";

    /// <summary>Codice ViaggiaTreno; vuoto finché non viene risolto.</summary>
    public string StationACode { get; set; } = "";

    [NotNull]
    public string StationBName { get; set; } = "";

    public string StationBCode { get; set; } = "";

    public int SortOrder { get; set; }

    [Ignore]
    public bool HasCodes => !string.IsNullOrWhiteSpace(StationACode) && !string.IsNullOrWhiteSpace(StationBCode);

    public RouteLeg GetLeg(Direction direction) => direction == Direction.AtoB
        ? new RouteLeg(StationAName, StationACode, StationBName, StationBCode)
        : new RouteLeg(StationBName, StationBCode, StationAName, StationACode);
}

public enum Direction
{
    AtoB,
    BtoA
}

/// <summary>Una tratta orientata: da origine a destinazione.</summary>
public sealed record RouteLeg(string FromName, string FromCode, string ToName, string ToCode)
{
    public string Label => $"{FromName} → {ToName}";
}
