namespace Bitakora.ControlAsistencia.Sedes.Entities;

// Mecanismo "declinar con resultado" (CA-ADR-0030): el aggregate nunca lanza -- retorna la razon
// del rechazo y el handler la traduce al status code. Exclusividad "un dispositivo a lo sumo en
// esta sede" evaluada solo con la historia del stream, sin verificacion cross-sede.
internal enum ResultadoInstalacionDispositivo
{
    Exitosa,
    YaInstalado
}
