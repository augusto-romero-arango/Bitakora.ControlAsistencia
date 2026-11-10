namespace Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction;

// Payload primitivo (MEF-ADR-0039 decision 6): nunca reusa un tipo de Colaboradores.DomainEvents
// como campo.
public record CorregirNombres(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string PrimerNombre,
    string? SegundoNombre,
    string PrimerApellido,
    string? SegundoApellido);
