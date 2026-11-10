namespace Bitakora.ControlAsistencia.Sedes.Entities;

// Declinar con resultado (CA-ADR-0030). Ninguna razon rechaza: dispositivo no instalado es estado ya
// alcanzado y sale por 204 sin evento, sin distinguir "ya retirado" de "nunca instalado": el id lo
// emite el dispositivo, nadie lo teclea, asi que un id desconocido no justifica un 404.
internal enum ResultadoRetiroDispositivo
{
    Exitosa,
    SinCambios
}
