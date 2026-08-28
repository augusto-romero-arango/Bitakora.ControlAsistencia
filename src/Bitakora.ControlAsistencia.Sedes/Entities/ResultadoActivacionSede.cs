namespace Bitakora.ControlAsistencia.Sedes.Entities;

// Issue #459: resultado de SedeAggregateRoot.Activar. Mecanismo "declinar con resultado"
// (CA-ADR-0030) -- activar una sede ya activa es la unica razon de rechazo, evaluable solo con el
// estado del stream, sin reloj. El handler la traduce a InvalidOperationException/409 (CA-3).
// internal: mismo criterio de visibilidad que ResultadoRetiroCentroDeCostos.
internal enum ResultadoActivacionSede
{
    Exitosa,
    YaActiva
}
