namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;

/// <summary>
/// Terna de identidad del colaborador tal como llega en el body de POST programacion/solicitudes.
/// </summary>
/// <remarks>
/// Issue #436 (fase B del corte): el body dejo de tipar con InformacionColaborador (PublicEvents),
/// que murio con este issue -- un DTO de request no es un evento que sale del bounded context, asi
/// que no pertenece al Published Language. Vive en el feature folder de su comando.
///
/// El cliente resuelve la terna contra el maestro Colaboradores y el servidor NUNCA lo consulta
/// (#330): Identificacion llega con el contrato "{Tipo}-{Numero}" y NombreCompleto ya concatenado.
/// El handler la pasa tal cual a los dos payloads (persistido y de bus), sin componer nada.
///
/// El nombre difiere a proposito de ColaboradorProgramado (Programacion.DomainEvents) y de
/// ResumenColaborador (PrivateEvents), sus dos gemelos de forma: un using equivocado entre islas
/// debe fallar la compilacion (CA-ADR-0029 decision #5).
/// </remarks>
public record ColaboradorSolicitado(
    string Identificacion,
    string CodigoColaborador,
    string NombreCompleto);
