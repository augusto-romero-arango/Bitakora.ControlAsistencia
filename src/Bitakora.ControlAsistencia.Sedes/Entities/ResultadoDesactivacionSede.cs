namespace Bitakora.ControlAsistencia.Sedes.Entities;

// Issue #459: resultado de SedeAggregateRoot.Desactivar. Mecanismo "declinar con resultado"
// (CA-ADR-0030) -- desactivar una sede ya inactiva es la unica razon de rechazo, evaluable solo con
// el estado del stream, sin reloj. El handler la traduce a InvalidOperationException/409 (CA-4).
// internal: mismo criterio de visibilidad que ResultadoRetiroCentroDeCostos.
internal enum ResultadoDesactivacionSede
{
    Exitosa,
    YaInactiva
}
