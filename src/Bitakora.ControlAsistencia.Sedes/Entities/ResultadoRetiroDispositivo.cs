namespace Bitakora.ControlAsistencia.Sedes.Entities;

// Mecanismo "declinar con resultado" (CA-ADR-0030): el aggregate nunca lanza -- retorna la razon
// del rechazo y el handler la traduce al status code. Dispositivo no instalado (ya retirado o
// nunca instalado) es estado ya alcanzado (MEF-ADR-0004): no-op exitoso, sin distinguir un caso
// del otro -- decision del experto (#664): el id lo emite el dispositivo, nadie lo teclea.
internal enum ResultadoRetiroDispositivo
{
    Exitosa,
    SinCambios
}
