namespace Bitakora.ControlAsistencia.Programacion.Entities;

// SinCambios no es rechazo sino el no-op "estado ya alcanzado" (MEF-ADR-0004): el endpoint responde
// 204 igual que ante un retiro real.
internal enum ResultadoRetiroTurno
{
    Retirado,
    SinCambios
}
