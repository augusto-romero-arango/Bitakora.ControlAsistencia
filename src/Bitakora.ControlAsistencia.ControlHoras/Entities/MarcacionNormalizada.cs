namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// TimestampNormalizado ya viene truncado al minuto desde RegistrarMarcacion.
// TipoMarcacion: ENTRADA, SALIDA o null cuando el dispositivo no reporta tipo.
// La sede llega despues del hecho crudo (MEF-ADR-0046): queda null hasta que EstamparSede la asocia.
public record MarcacionNormalizada(
    DateTime TimestampNormalizado,
    string? TipoMarcacion,
    string? DispositivoId = null,
    string? CodigoSede = null,
    string? NombreSede = null,
    string? CentroDeCostos = null);
