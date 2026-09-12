namespace Bitakora.ControlAsistencia.Sedes.Entities;

// Mecanismo "declinar con resultado" (CA-ADR-0030): el aggregate nunca lanza -- retorna la razon
// del rechazo y el handler la traduce al status code. Sin CC vigente es estado ya alcanzado
// (MEF-ADR-0004): no-op exitoso, el handler no lanza y el endpoint responde 204 sin evento.
// internal: mismo criterio de visibilidad que los resultados hermanos de Colaboradores.
internal enum ResultadoRetiroCentroDeCostos
{
    Exitosa,
    SinCambios
}
