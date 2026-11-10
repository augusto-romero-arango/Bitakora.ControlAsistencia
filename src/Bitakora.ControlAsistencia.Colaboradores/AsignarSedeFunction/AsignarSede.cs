namespace Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction;

// Payload primitivo (MEF-ADR-0039 decision 6): nunca reusa un tipo de Colaboradores.DomainEvents ni
// de Sedes.DomainEvents (islas, CA-ADR-0029) como campo.
public record AsignarSede(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string CodigoSede);
