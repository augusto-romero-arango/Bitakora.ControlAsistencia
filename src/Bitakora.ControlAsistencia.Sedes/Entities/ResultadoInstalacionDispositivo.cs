namespace Bitakora.ControlAsistencia.Sedes.Entities;

// Mecanismo "declinar con resultado" (CA-ADR-0030): el aggregate nunca lanza -- retorna la razon
// del rechazo y el handler la traduce al status code. Exclusividad "un dispositivo a lo sumo en
// esta sede" evaluada solo con la historia del stream, sin verificacion cross-sede (issue #460,
// decision de sesion 2026-08-27: confianza en el cliente, la correccion es retirar).
// internal: mismo criterio de visibilidad que los resultados hermanos del dominio.
internal enum ResultadoInstalacionDispositivo
{
    Exitosa,
    YaInstalado
}
