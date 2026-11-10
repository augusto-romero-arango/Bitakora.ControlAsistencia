namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Payload propio de esta isla, espejo de PrivateEvents.ControlHoras.MarcacionDelDia
// (payload por rol, MEF-ADR-0039 decision #6).
//
// La sede marcada viaja por marcacion, no por franja: entrada y salida de una misma franja pueden
// venir de dispositivos de sedes distintas. Sede y Pertenencia son null en los streams previos a
// su alta (MEF-ADR-0036); la sede ademas llega despues del hecho crudo (estampado, MEF-ADR-0046).
// La lectura legada de Pertenencia vive en DiaCalculadoAggregateRoot.GenerarDepuracionDelDia.
public record MarcacionDelDia(
    DateTime Timestamp,
    string? Tipo,
    string? CodigoSede = null,
    string? NombreSede = null,
    string? CentroDeCostos = null,
    string? Pertenencia = null);
