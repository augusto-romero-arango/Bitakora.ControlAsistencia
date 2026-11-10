namespace Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction;

// Payload primitivo (MEF-ADR-0039 decision 6): nunca reusa un tipo de Colaboradores.DomainEvents
// como campo.
// FechaCorregida es REQUERIDA, sin default del servidor: el tiempo de los hechos viene del
// cliente, nunca del reloj del servidor.
public record CorregirFechaInicioVinculacion(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Codigo,
    DateOnly FechaCorregida);
