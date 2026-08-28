namespace Bitakora.ControlAsistencia.Sedes.Entities;

// Issue #458: resultado de SedeAggregateRoot.RetirarCentroDeCostos. Mecanismo "declinar con
// resultado" (CA-ADR-0030) -- retirar sin CC vigente es la unica razon de rechazo, evaluable solo
// con el estado del stream, sin reloj. El handler la traduce a InvalidOperationException/409 (CA-4,
// propuesta revisable segun el issue).
// internal: mismo criterio de visibilidad que los resultados hermanos de Colaboradores
// (ResultadoAsignacionEtiqueta/ResultadoRetiroEtiqueta) -- vive en el mismo ensamblado que el
// handler que lo consume.
internal enum ResultadoRetiroCentroDeCostos
{
    Exitosa,
    SinCentroDeCostosVigente
}
