namespace Bitakora.ControlAsistencia.Sedes.Entities;

// Mecanismo "declinar con resultado" (CA-ADR-0030): el aggregate nunca lanza -- retorna la razon
// del rechazo y el handler la traduce al status code. Unica razon aqui, evaluable solo con el
// estado del stream y sin reloj.
// internal: mismo criterio de visibilidad que los resultados hermanos de Colaboradores.
internal enum ResultadoRetiroCentroDeCostos
{
    Exitosa,
    SinCentroDeCostosVigente
}
