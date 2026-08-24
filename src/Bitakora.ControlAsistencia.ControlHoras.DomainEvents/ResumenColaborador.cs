namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Payload propio de esta isla, espejo de PrivateEvents.Colaboradores.ResumenColaborador (payload
// por rol, MEF-ADR-0039 decision #6). Terna de identidad que viaja en DepuracionDiaRecibida.
// Comparte forma con ColaboradorProgramado (payload de TurnoDiarioAsignado, misma isla): son dos
// eventos persistidos distintos, cada uno dueno de su payload, y se mantienen separados por
// MEF-ADR-0018 (Rule of Three) -- unificarlos amarra la evolucion de un evento a la del otro.
public record ResumenColaborador(
    string Identificacion,
    string CodigoColaborador,
    string NombreCompleto);
