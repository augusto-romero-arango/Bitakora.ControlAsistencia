namespace Bitakora.ControlAsistencia.Sedes.Entities;

// Mecanismo "declinar con resultado" (CA-ADR-0030): el aggregate nunca lanza -- retorna el
// resultado y el handler decide. Hoy ninguna razon se traduce a un status code de rechazo: sin CC
// vigente es estado ya alcanzado (MEF-ADR-0004) y sale por el mismo 204 sin evento del camino de
// cambio. Agregar aqui una razon que SI rechace obliga a reintroducir su traduccion en el handler.
// internal: mismo criterio de visibilidad que los resultados hermanos de Colaboradores.
internal enum ResultadoRetiroCentroDeCostos
{
    Exitosa,
    SinCambios
}
