namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Payload plano y crudo de UNA marcacion del dia. Sin marca usada/descartada a proposito: el receptor
// la deriva comparando el Timestamp contra la Entrada o la Salida de cada FranjaDepurada, y la
// idempotencia por minuto del aggregate garantiza que esos timestamps son unicos.
//
// La Pertenencia (DelTurno, FueraDeCobertura, Ambigua o DeTurnoVecino) si viaja: el receptor no puede
// derivarla porque exige la cobertura propia y la de los turnos vecinos, que solo conoce
// ControlDiario. En un dia sin jornada valida solo puede ser DeTurnoVecino; las demas van en null.
//
// La sede MARCADA viaja POR MARCACION, no por franja: entrada y salida de una misma franja pueden
// venir de dispositivos de sedes distintas, y elegir cual representa la franja ya seria un juicio
// del expediente. Null hasta que llega el estampado coreografiado (MEF-ADR-0046), posterior al
// hecho crudo.
public record MarcacionDelDia(
    DateTime Timestamp,
    string? Tipo,
    string? CodigoSede = null,
    string? NombreSede = null,
    string? CentroDeCostos = null,
    string? Pertenencia = null);
