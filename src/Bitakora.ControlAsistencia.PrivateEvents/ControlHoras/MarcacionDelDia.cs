namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Payload plano y crudo de UNA marcacion del dia. Sin marca usada/descartada a proposito: el receptor
// la deriva comparando el Timestamp contra la Entrada o la Salida de cada FranjaDepurada, y la
// idempotencia por minuto del aggregate garantiza que esos timestamps son unicos.
public record MarcacionDelDia(DateTime Timestamp, string? Tipo);
