namespace Bitakora.ControlAsistencia.Sedes.Entities;

// Mecanismo "declinar con resultado" (CA-ADR-0030): el aggregate nunca lanza -- retorna el
// resultado y el handler decide. Hoy ninguna razon se traduce a un status code de rechazo:
// dispositivo no instalado es estado ya alcanzado (MEF-ADR-0004) y sale por el mismo 204 sin evento
// del camino de cambio, sin distinguir "ya retirado" de "nunca instalado" -- el id lo emite el
// dispositivo al instalarse, nadie lo teclea, asi que un id desconocido no justifica un 404.
internal enum ResultadoRetiroDispositivo
{
    Exitosa,
    SinCambios
}
