namespace Bitakora.ControlAsistencia.Contracts.ValueObjects;

/// <summary>
/// Abstraccion compartida para todas las franjas temporales de un turno.
/// CA-1: Encapsula HoraInicio, HoraFin, calculo de duracion y ToString.
/// CA-6: Semantica inicio-inclusivo, fin-exclusivo.
/// </summary>
public abstract record FranjaBase
{
    public TimeOnly HoraInicio { get; }
    public TimeOnly HoraFin { get; }

    /// <summary>
    /// Offset del dia de fin (0 = mismo dia, 1 = dia siguiente).
    /// Usado por ToString() para producir el formato correcto.
    /// </summary>
    public abstract int DiaOffsetFin { get; }

    /// <summary>
    /// Minutos transcurridos desde el inicio del dia base (dia 0),
    /// considerando DiaOffsetInicio cuando aplica.
    /// Permite comparacion temporal uniforme entre franjas con distinto offset.
    /// </summary>
    public abstract int MinutosAbsolutoInicio { get; }

    /// <summary>
    /// Minutos transcurridos desde el inicio del dia base (dia 0),
    /// considerando DiaOffsetFin.
    /// </summary>
    public abstract int MinutosAbsolutoFin { get; }

    /// <summary>
    /// CA-10 / CA-11: Duracion en minutos considerando offsets de dia.
    /// </summary>
    public abstract int DuracionEnMinutos();

    /// <summary>
    /// CA-12: Duracion como horas en formato decimal.
    /// </summary>
    public abstract double DuracionEnHorasDecimales();

    protected FranjaBase(TimeOnly horaInicio, TimeOnly horaFin)
    {
        HoraInicio = horaInicio;
        HoraFin = horaFin;
    }

    // CA-9: constructor vacio para compatibilidad con Marten/JSON
    protected FranjaBase() { }

    /// <summary>
    /// CA-13: Infiere el offset de dia para el fin cuando fin es menor que inicio.
    /// Retorna 1 si fin &lt; inicio, 0 en caso contrario.
    /// </summary>
    public static int InferirDiaOffset(TimeOnly inicio, TimeOnly fin)
        => fin < inicio ? 1 : 0;

    /// <summary>
    /// CA-20: Formato legible. Ej: "(06:00-12:00)" o "(22:00-06:00+1)" con offset.
    /// CA-21: sealed evita que records derivados generen su propio ToString automaticamente.
    /// </summary>
    public sealed override string ToString()
    {
        var sufijo = DiaOffsetFin > 0 ? $"+{DiaOffsetFin}" : "";
        return $"({HoraInicio:HH:mm}-{HoraFin:HH:mm}{sufijo})";
    }
}
