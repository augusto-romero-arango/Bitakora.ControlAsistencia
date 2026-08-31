namespace Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction;

// Payload primitivo (MEF-ADR-0039 decision 6): nunca reusa un tipo de Colaboradores.DomainEvents ni
// de Sedes.DomainEvents (islas, CA-ADR-0029) como campo. El endpoint lo compone desde el {id} de
// ruta ya parseado + el CodigoSede del body.
public record AsignarSede(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string CodigoSede);
