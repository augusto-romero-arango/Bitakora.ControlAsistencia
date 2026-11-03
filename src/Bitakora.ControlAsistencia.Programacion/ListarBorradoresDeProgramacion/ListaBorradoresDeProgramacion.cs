using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.ListarBorradoresDeProgramacion;

public sealed record DiaPendiente(
    DateOnly Fecha,
    string NombreTurno,
    string HorarioResumido,
    IReadOnlyList<Bloque> Bloques,
    bool EsHorarioAbierto,
    bool Vencido);

public sealed record BorradorDeColaborador(
    string CodigoColaborador,
    string Identificacion,
    string NombreCompleto,
    IReadOnlyList<DiaPendiente> Dias);

public sealed record ListaBorradoresDeProgramacion(
    IReadOnlyList<BorradorDeColaborador> Elementos,
    string? SiguienteCursor = null)
{
    public static ListaBorradoresDeProgramacion Componer(
        DateOnly fechaReferencia, DateOnly? desde, DateOnly? hasta,
        IEnumerable<BorradorDeProgramacion> borradores,
        int? take = null, CursorBorradoresDeProgramacion? cursor = null) =>
        throw new NotImplementedException();
}
