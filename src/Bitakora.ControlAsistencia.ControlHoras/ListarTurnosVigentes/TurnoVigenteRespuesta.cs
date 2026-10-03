using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;

/// <summary>
/// Forma de respuesta de la programacion vigente de un dia (MEF-ADR-0041 decision 4): igual que
/// <see cref="TurnoVigente"/> pero sin el turno que una ausencia cubre, que es estado interno del
/// documento. Un dia de ausencia trae MotivoAusencia y Bloques vacio.
/// </summary>
public sealed record TurnoVigenteRespuesta(
    string Id,
    string CodigoColaborador,
    string NombreCompleto,
    DateOnly Fecha,
    string NombreTurno,
    string HorarioResumido,
    IReadOnlyList<Bloque> Bloques,
    string? MotivoAusencia)
{
    public static TurnoVigenteRespuesta Desde(TurnoVigente v) =>
        new(v.Id, v.CodigoColaborador, v.NombreCompleto, v.Fecha, v.NombreTurno,
            v.HorarioResumido, v.Bloques, v.MotivoAusencia);
}
