namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Issue #424: payload plano y crudo de UNA marcacion del dia, sin marca usada/descartada -- el
// receptor la deriva (usada <=> el Timestamp coincide con Entrada o Salida de alguna FranjaDepurada;
// la idempotencia por minuto garantiza timestamps unicos). Es la evidencia cruda sobre la que el
// Aprobador juzga (#429) y la unica prueba de las anomalias del dia sin jornada valida.
//
// Todos los campos son primitivos -> la igualdad por valor del record por defecto ya es correcta, sin
// Equals/GetHashCode propios (MEF-ADR-0012).
public record MarcacionDelDia(DateTime Timestamp, string? Tipo);
