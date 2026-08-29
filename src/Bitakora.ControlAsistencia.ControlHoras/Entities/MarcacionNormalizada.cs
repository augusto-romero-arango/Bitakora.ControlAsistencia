namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// HU-106: Value object que representa una marcacion normalizada dentro del ControlDiario
// TimestampNormalizado ya viene truncado al minuto desde RegistrarMarcacion (#105)
// TipoMarcacion es opcional (ENTRADA, SALIDA o null cuando el dispositivo no reporta tipo)
// Issue #463: DispositivoId (ya presente en MarcacionAdicionada) y el estampado de sede se agregan
// como parametros opcionales al final para no romper los usos posicionales de 2 argumentos
// existentes (DepuradorDeMarcacionesTests). CodigoSede/NombreSede/CentroDeCostos quedan null hasta
// que EstamparSede los asocia a la marcacion que coincide por TimestampNormalizado+DispositivoId.
public record MarcacionNormalizada(
    DateTime TimestampNormalizado,
    string? TipoMarcacion,
    string? DispositivoId = null,
    string? CodigoSede = null,
    string? NombreSede = null,
    string? CentroDeCostos = null);
