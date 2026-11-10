using TrenordTratta.Core.Models;

namespace TrenordTratta.Core.Services;

/// <summary>
/// Direzione automatica: prima dell'ora di cambio A→B (andata), dopo B→A (ritorno).
/// L'utente può invertire a mano; l'inversione vale finché non si cambia "fascia" (mattina/pomeriggio).
/// </summary>
public sealed class DirectionResolver
{
    private Direction? _manual;
    private Direction? _autoWhenManualSet;

    public static Direction Auto(DateTime now, int switchHour) =>
        now.Hour < switchHour ? Direction.AtoB : Direction.BtoA;

    public Direction Resolve(DateTime now, int switchHour)
    {
        var auto = Auto(now, switchHour);
        if (_manual is not null && _autoWhenManualSet == auto) return _manual.Value;

        _manual = null;
        _autoWhenManualSet = null;
        return auto;
    }

    public Direction Toggle(DateTime now, int switchHour)
    {
        var current = Resolve(now, switchHour);
        _manual = current == Direction.AtoB ? Direction.BtoA : Direction.AtoB;
        _autoWhenManualSet = Auto(now, switchHour);
        return _manual.Value;
    }

    public bool IsManual => _manual is not null;
}
