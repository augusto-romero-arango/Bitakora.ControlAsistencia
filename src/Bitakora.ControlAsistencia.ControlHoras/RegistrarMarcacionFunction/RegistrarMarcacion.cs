namespace Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction;

public record RegistrarMarcacion(
    string CodigoColaborador,
    DateTime Timestamp,
    string? TipoMarcacion,
    string? DispositivoId);
