namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Payload propio de esta isla, espejo de PrivateEvents.Colaboradores.ResumenColaborador (payload
// por rol, MEF-ADR-0039 decision #6). Comparte forma con ColaboradorProgramado pero se mantienen
// separados (MEF-ADR-0018): unificarlos amarra la evolucion de un evento a la del otro.
public record ResumenColaborador(
    string Identificacion,
    string CodigoColaborador,
    string NombreCompleto);
