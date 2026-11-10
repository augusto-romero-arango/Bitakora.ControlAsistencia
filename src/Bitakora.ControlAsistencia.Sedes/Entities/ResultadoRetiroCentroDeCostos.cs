namespace Bitakora.ControlAsistencia.Sedes.Entities;

// Declinar con resultado (CA-ADR-0030). Ninguna razon rechaza: sin CC vigente es estado ya alcanzado
// y sale por 204 sin evento. Si agregas una razon que rechace, agrega su traduccion en el handler.
internal enum ResultadoRetiroCentroDeCostos
{
    Exitosa,
    SinCambios
}
