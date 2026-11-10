namespace Bitakora.ControlAsistencia.Sedes.Entities;

// Declinar con resultado (CA-ADR-0030): el aggregate no lanza; el handler traduce la razon a status code.
// YaInstalado mira solo esta sede: la exclusividad cross-sede no se verifica aqui.
internal enum ResultadoInstalacionDispositivo
{
    Exitosa,
    YaInstalado
}
