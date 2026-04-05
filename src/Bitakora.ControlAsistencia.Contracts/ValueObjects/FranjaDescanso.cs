namespace Bitakora.ControlAsistencia.Contracts.ValueObjects;

// Pausa operativa dentro de una franja ordinaria (ej. almuerzo).
// Es hoja - no contiene otras franjas.
// CA-5: tiene DiaOffsetInicio y DiaOffsetFin
// ADR-0015: record con factory estatico, constructor privado.
public sealed record FranjaDescanso : FranjaTemporal
{
    // Constructor vacio privado para compatibilidad con Marten/JSON (CA-9)
    private FranjaDescanso() { }

    // CA-5: offsets respecto al dia de la ordinaria padre
    public int DiaOffsetInicio { get; private init; }
    public int DiaOffsetFin { get; private init; }

    // CA-8: factory estatico
    // CA-7: rechaza InicioYFinIguales
    public static FranjaDescanso Crear(TimeOnly horaInicio, TimeOnly horaFin,
        int diaOffsetInicio = 0, int diaOffsetFin = 0)
        => throw new NotImplementedException();

    // CA-20, CA-21: formato heredado de la base
    public override string ToString() => throw new NotImplementedException();
}
