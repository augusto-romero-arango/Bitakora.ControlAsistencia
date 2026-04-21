namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// HU-106: Value object que representa una marcacion normalizada dentro del ControlDiario
// TimestampNormalizado ya viene truncado al minuto desde RegistrarMarcacion (#105)
// TipoMarcacion es opcional (ENTRADA, SALIDA o null cuando el dispositivo no reporta tipo)
public record MarcacionNormalizada(DateTime TimestampNormalizado, string? TipoMarcacion);
