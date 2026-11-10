namespace Bitakora.ControlAsistencia.Sedes.Entities;

// Declinar con resultado (CA-ADR-0030): el aggregate no lanza; el handler traduce la razon a status code.
internal enum ResultadoActivacionSede
{
    Exitosa,
    YaActiva
}
