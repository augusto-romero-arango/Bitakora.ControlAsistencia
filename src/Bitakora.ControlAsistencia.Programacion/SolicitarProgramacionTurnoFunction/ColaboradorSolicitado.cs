namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;

/// <summary>
/// Terna de identidad tal como llega en el body de POST programacion/solicitudes. El cliente la
/// resuelve contra el maestro Colaboradores y el servidor NUNCA lo consulta: Identificacion llega como
/// "{Tipo}-{Numero}" y NombreCompleto ya concatenado; el handler la pasa tal cual a los dos payloads.
/// </summary>
/// <remarks>
/// El nombre difiere a proposito de ColaboradorProgramado y ResumenColaborador, sus gemelos de forma:
/// un using equivocado entre islas debe fallar la compilacion (CA-ADR-0029).
/// </remarks>
public record ColaboradorSolicitado(
    string Identificacion,
    string CodigoColaborador,
    string NombreCompleto);
