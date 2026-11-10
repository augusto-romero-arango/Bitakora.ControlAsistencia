namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Tramo de turno en minutos absolutos desde la medianoche de la fecha de asignacion.
/// Representacion intermedia de <see cref="TurnoDiario.Segmentar"/>: la aritmetica de offsets de
/// dia se resuelve en enteros y solo al final se traduce a instantes (<see cref="ResolverA"/>).
/// </summary>
/// <remarks>
/// <c>internal</c> a proposito: no es payload de ningun evento persistido, es detalle del algoritmo.
/// La aritmetica replica la de <c>MomentoDelDia</c> (ControlHoras/ValueObjects) en vez de reusarla:
/// aquel vive en el Function App, que referencia este ensamblado y no al reves (CA-ADR-0029).
///
/// Sede viaja como dato inerte por toda la cadena: la estampa la franja madre en un unico paso al
/// cierre de <see cref="FranjaProgramada.Segmentar"/>; por eso ningun <c>new Tramo(...)</c> de la
/// aritmetica de recorte necesita pasarla.
/// </remarks>
internal readonly record struct Tramo(TipoBloque Tipo, int Inicio, int Fin, SedeProgramada? Sede = null)
{
    private const int MinutosPorHora = 60;
    private const int MinutosPorDia = 1440;

    internal static int MinutosAbsolutos(TimeOnly hora, int diaOffset) =>
        hora.Hour * MinutosPorHora + hora.Minute + diaOffset * MinutosPorDia;

    internal static (TimeOnly Hora, int DiaOffset) DesdeMinutosAbsolutos(int minutos) =>
        (new TimeOnly(minutos % MinutosPorDia / MinutosPorHora, minutos % MinutosPorHora), minutos / MinutosPorDia);

    /// <summary>
    /// Rompe el tramo en cada medianoche que cruce: ningun tramo resultante abarca mas de un
    /// dia calendario. Un tramo que arranca exactamente a medianoche no se parte en su propio
    /// inicio.
    /// </summary>
    internal IEnumerable<Tramo> RomperEnMedianoche()
    {
        var cursor = Inicio;
        var primerLimite = (Inicio / MinutosPorDia + 1) * MinutosPorDia;
        for (var limite = primerLimite; limite < Fin; limite += MinutosPorDia)
        {
            yield return this with { Inicio = cursor, Fin = limite };
            cursor = limite;
        }

        yield return this with { Inicio = cursor };
    }

    /// <summary>
    /// Resuelve el tramo a un <see cref="BloqueTurno"/> con instantes absolutos, contra
    /// <paramref name="fecha"/> (el dia de asignacion del turno).
    /// </summary>
    internal BloqueTurno ResolverA(DateOnly fecha)
    {
        var medianoche = fecha.ToDateTime(TimeOnly.MinValue);
        return new BloqueTurno(Tipo, medianoche.AddMinutes(Inicio), medianoche.AddMinutes(Fin), Sede);
    }
}
