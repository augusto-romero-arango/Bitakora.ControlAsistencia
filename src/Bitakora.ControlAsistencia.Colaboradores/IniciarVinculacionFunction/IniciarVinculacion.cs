namespace Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction;

// Payload primitivo (MEF-ADR-0039 decision 6): nunca reusa un tipo de Colaboradores.DomainEvents
// como campo.
// Sin nombres (corregir nombres es otra intencion) ni motivo (el "por que" es de RRHH/nomina).
// FechaInicio es REQUERIDA, sin default del servidor: el tiempo de los hechos viene del cliente.
public record IniciarVinculacion(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string CodigoColaborador,
    DateOnly FechaInicio,
    string? CodigoSede = null);
