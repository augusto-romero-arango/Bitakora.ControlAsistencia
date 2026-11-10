namespace Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction;

// Payload primitivo (MEF-ADR-0039 decision 6): nunca reusa un tipo de Colaboradores.DomainEvents
// como campo.
// FechaEfectiva es REQUERIDA y puede ser pasada (registro tardio) o futura (preaviso): nunca se
// valida contra el reloj del servidor.
public record TerminarVinculacion(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Codigo,
    DateOnly FechaEfectiva);
