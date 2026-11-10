namespace Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction;

// CA-5: stream ID determinista: {CodigoColaborador}:{Timestamp:yyyy-MM-ddTHH:mm:ss}
public record RegistrarMarcacion(
    string CodigoColaborador,
    DateTime Timestamp,
    string? TipoMarcacion,
    string? DispositivoId);
