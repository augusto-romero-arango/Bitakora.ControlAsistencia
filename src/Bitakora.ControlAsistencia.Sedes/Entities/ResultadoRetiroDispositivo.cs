namespace Bitakora.ControlAsistencia.Sedes.Entities;

// Mecanismo "declinar con resultado" (CA-ADR-0030): el aggregate nunca lanza -- retorna la razon
// del rechazo y el handler la traduce al status code. A diferencia de
// ResultadoRetiroCentroDeCostos (VO singular -> 409), NoInstalado se traduce a 404: el
// dispositivo-id de la ruta direcciona un sub-recurso de una coleccion que no existe en esta sede.
internal enum ResultadoRetiroDispositivo
{
    Exitosa,
    NoInstalado
}
