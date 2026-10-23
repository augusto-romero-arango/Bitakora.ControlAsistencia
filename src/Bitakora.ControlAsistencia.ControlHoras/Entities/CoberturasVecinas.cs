namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Resoluciones: marcacion -> dia al que el Aprobador la asigno. Solo pesan sobre marcaciones que
// siguen en el solape con un vecino; fuera de el rige la regla normal.
public record CoberturasVecinas(
    DateTime? FinDeLaVispera,
    DateTime? InicioDelDiaSiguiente,
    DateOnly? Dia = null,
    IReadOnlyDictionary<DateTime, DateOnly>? Resoluciones = null)
{
    public bool Excluye(DateTime instante) =>
        EnSolape(instante) && !(Resolucion(instante) == Dia && Dia is not null);

    public bool Cede(DateTime instante) =>
        EnSolape(instante) && Resolucion(instante) is { } fecha && fecha != Dia;

    private bool EnSolape(DateTime instante) =>
        instante < FinDeLaVispera || instante >= InicioDelDiaSiguiente;

    private DateOnly? Resolucion(DateTime instante) =>
        Resoluciones is not null && Resoluciones.TryGetValue(instante, out var fecha) ? fecha : null;
}
