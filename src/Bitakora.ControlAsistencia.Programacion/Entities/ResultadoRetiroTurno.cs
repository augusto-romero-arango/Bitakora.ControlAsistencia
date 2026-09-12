namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Mecanismo "declinar con resultado" (CA-ADR-0030): el aggregate nunca lanza. SinCambios no es un
// rechazo sino el no-op de MEF-ADR-0004 ("estado ya alcanzado"): el handler no lo traduce a nada y
// el endpoint responde 204 igual que ante un retiro real.
internal enum ResultadoRetiroTurno
{
    Retirado,
    SinCambios
}
