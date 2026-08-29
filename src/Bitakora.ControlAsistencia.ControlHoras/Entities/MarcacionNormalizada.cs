namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// HU-106: Value object que representa una marcacion normalizada dentro del ControlDiario
// TimestampNormalizado ya viene truncado al minuto desde RegistrarMarcacion (#105)
// TipoMarcacion es opcional (ENTRADA, SALIDA o null cuando el dispositivo no reporta tipo)
// El estampado de sede llega despues del hecho crudo (enriquecimiento coreografiado, MEF-ADR-0046):
// CodigoSede/NombreSede/CentroDeCostos quedan null hasta que EstamparSede los asocia.
public record MarcacionNormalizada(
    DateTime TimestampNormalizado,
    string? TipoMarcacion,
    string? DispositivoId = null,
    string? CodigoSede = null,
    string? NombreSede = null,
    string? CentroDeCostos = null);
