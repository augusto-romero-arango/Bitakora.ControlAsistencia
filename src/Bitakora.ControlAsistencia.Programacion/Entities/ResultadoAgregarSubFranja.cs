namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Mecanismo "declinar con resultado" (CA-ADR-0030): el aggregate nunca lanza -- retorna la razon
// del rechazo y el handler la traduce al status code (409 Conflict).
// Precedencia: TurnoRetirado > TurnoEsDescanso > FranjaNoExiste.
internal enum ResultadoAgregarSubFranja
{
    Agregada,
    TurnoRetirado,
    TurnoEsDescanso,
    FranjaNoExiste
}
