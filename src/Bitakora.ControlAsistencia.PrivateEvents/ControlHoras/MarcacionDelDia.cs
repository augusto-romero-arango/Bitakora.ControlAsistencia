namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Payload plano y crudo de UNA marcacion del dia. Sin marca usada/descartada a proposito: el receptor
// la deriva comparando el Timestamp contra la Entrada o la Salida de cada FranjaDepurada, y la
// idempotencia por minuto del aggregate garantiza que esos timestamps son unicos.
//
// Issue #464: la sede MARCADA viaja plana desde MarcacionNormalizada, POR MARCACION (no por franja):
// entrada y salida de una misma franja pueden venir de dispositivos de sedes distintas, y elegir
// cual representa la franja ya seria un juicio del expediente (#482). Null cuando el estampado
// coreografiado (#463, MEF-ADR-0046) aun no llego -- enriquecimiento posterior al hecho crudo.
public record MarcacionDelDia(
    DateTime Timestamp,
    string? Tipo,
    string? CodigoSede = null,
    string? NombreSede = null,
    string? CentroDeCostos = null);
