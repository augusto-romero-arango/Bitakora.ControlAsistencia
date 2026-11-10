namespace Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction;

// Payload primitivo (MEF-ADR-0039 decision 6): nunca reusa un tipo de Colaboradores.DomainEvents
// como campo.
public record AnularTerminacion(string TipoIdentificacion, string NumeroIdentificacion, string Codigo);
