namespace Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction;

// Payload primitivo (MEF-ADR-0039 decision 6): nunca reusa un tipo de Colaboradores.DomainEvents
// como campo.
// FechaInicio es REQUERIDA, sin default del servidor: el tiempo de los hechos viene del cliente
// (la migracion trae fechas pasadas).
public record RegistrarColaborador(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string PrimerNombre,
    string? SegundoNombre,
    string PrimerApellido,
    string? SegundoApellido,
    string CodigoColaborador,
    DateOnly FechaInicio,
    string? CodigoSede = null);
