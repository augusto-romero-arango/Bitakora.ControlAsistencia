namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Payload propio de esta isla, espejo de PrivateEvents.ControlHoras.MarcacionDelDia
// (payload por rol, MEF-ADR-0039 decision #6).
//
// La sede MARCADA viaja POR MARCACION, no por franja: entrada y salida de una misma franja pueden
// venir de dispositivos de sedes distintas. Sede y Pertenencia son opcionales porque se agregaron
// sobre un evento ya persistido: los streams previos deserializan con null (MEF-ADR-0036). La
// lectura legada de Pertenencia vive en DiaCalculadoAggregateRoot.GenerarDepuracionDelDia; la sede
// ademas llega despues del hecho crudo (estampado coreografiado, MEF-ADR-0046).
public record MarcacionDelDia(
    DateTime Timestamp,
    string? Tipo,
    string? CodigoSede = null,
    string? NombreSede = null,
    string? CentroDeCostos = null,
    string? Pertenencia = null);
