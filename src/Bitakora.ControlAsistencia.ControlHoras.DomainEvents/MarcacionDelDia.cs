namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Payload propio de esta isla, espejo de PrivateEvents.ControlHoras.MarcacionDelDia
// (payload por rol, MEF-ADR-0039 decision #6).
//
// Issue #484: la sede MARCADA viaja POR MARCACION, no por franja -- entrada y salida de una misma
// franja pueden venir de dispositivos de sedes distintas. Opcional por defecto por dos razones que
// se suman: el estampado coreografiado (MEF-ADR-0046) llega despues del hecho crudo, y el cambio es
// ADITIVO sobre un evento ya persistido -- los streams previos deserializan con null (MEF-ADR-0036).
public record MarcacionDelDia(
    DateTime Timestamp,
    string? Tipo,
    string? CodigoSede = null,
    string? NombreSede = null,
    string? CentroDeCostos = null);
