namespace Bitakora.ControlAsistencia.Programacion.CancelarProgramacionFunction;

/// <summary>
/// Terna de identidad del colaborador tal como llega en el body de POST programacion/cancelaciones.
/// </summary>
/// <remarks>
/// Copia propia del feature folder, no un reuso de SolicitarProgramacionTurnoFunction.ColaboradorSolicitado:
/// cada comando posee su propio DTO de request (mismo criterio documentado en ese tipo), asi un
/// cambio de forma en un comando no arrastra al otro.
/// </remarks>
public record ColaboradorSolicitado(
    string Identificacion,
    string CodigoColaborador,
    string NombreCompleto);

public record CancelarProgramacion(
    Guid Id,
    ColaboradorSolicitado Colaborador,
    List<DateOnly> Fechas);
