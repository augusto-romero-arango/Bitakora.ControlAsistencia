namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

// Espejo textual del enum TipoSubFranja del dominio Programacion: sumar un valor aqui exige que el
// dominio ya lo acepte.
internal static class TipoSubFranja
{
    public const string Descanso = "descanso";

    public const string Extra = "extra";

    public static bool TryNormalizar(string valor, out string tipo)
    {
        tipo = valor.Trim().ToLowerInvariant();
        return tipo is Descanso or Extra;
    }
}
