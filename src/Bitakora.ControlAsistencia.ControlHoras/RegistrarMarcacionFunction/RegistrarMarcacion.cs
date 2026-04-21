namespace Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction;

// HU-105: Comando para registrar una marcacion de entrada o salida de un empleado
// Trigger: HTTP POST, Route: control-horas/marcaciones
// CA-5: stream ID determinista: {EmpleadoId}:{Timestamp:yyyy-MM-ddTHH:mm:ss}
public record RegistrarMarcacion(
    string EmpleadoId,
    DateTime Timestamp,
    string? TipoMarcacion,
    string? DispositivoId);
