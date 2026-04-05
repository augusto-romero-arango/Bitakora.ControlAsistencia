namespace Bitakora.ControlAsistencia.Contracts.ValueObjects;

// Segmento continuo de trabajo dentro de un turno.
// Puede contener FranjaDescanso y FranjaExtra.
// CA-4: tiene DiaOffsetFin (0 = mismo dia, 1 = dia siguiente)
// ADR-0015: record con factory estatico, constructor privado.
public sealed record FranjaOrdinaria : FranjaTemporal
{
    // Constructor vacio privado para compatibilidad con Marten/JSON (CA-9)
    private FranjaOrdinaria() { }

    // CA-4: offset del fin respecto al dia de inicio
    public int DiaOffsetFin { get; private init; }

    public IReadOnlyList<FranjaDescanso> Descansos { get; private init; } = [];
    public IReadOnlyList<FranjaExtra> Extras { get; private init; } = [];

    // CA-8: factory estatico
    // CA-7: rechaza InicioYFinIguales
    // CA-13: infiere offset +1 cuando fin < inicio
    // CA-14 a CA-16: valida que descansos y extras esten contenidos
    // CA-17 a CA-19: valida que descansos y extras no se solapen entre si
    public static FranjaOrdinaria Crear(
        TimeOnly horaInicio,
        TimeOnly horaFin,
        int diaOffsetFin = 0,
        IEnumerable<FranjaDescanso>? descansos = null,
        IEnumerable<FranjaExtra>? extras = null)
        => throw new NotImplementedException();

    // CA-20, CA-21: formato "(06:00-12:00)" o "(22:00-06:00+1)"
    public override string ToString() => throw new NotImplementedException();
}
