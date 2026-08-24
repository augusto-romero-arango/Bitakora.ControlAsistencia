namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Issue #425: payload propio de esta isla, espejo de PrivateEvents.Colaboradores.ResumenColaborador
// (payload por rol, MEF-ADR-0039 decision #6). Terna reducida de identidad del colaborador que
// viaja en DepuracionDiaRecibida -- distinta de ColaboradorProgramado (foto completa del maestro
// que ya usa TurnoDiarioAsignado en esta misma isla).
public record ResumenColaborador(
    string Identificacion,
    string CodigoColaborador,
    string NombreCompleto);
