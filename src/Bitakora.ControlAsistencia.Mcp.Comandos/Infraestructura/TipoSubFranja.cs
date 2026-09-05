namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

// Vocabulario cerrado del parametro `tipo` de las tools de sub-franja, espejo del enum
// TipoSubFranja del dominio Programacion: se replica el texto, no el tipo (MEF-ADR-0047
// decision 3). Sumar un valor aqui exige que el dominio ya lo acepte.
internal static class TipoSubFranja
{
    public const string Descanso = "descanso";

    public const string Extra = "extra";

    /// <summary>
    /// Normaliza a minusculas y confirma que sea uno de los dos tipos aceptados; el mensaje de
    /// rechazo lo formatea cada tool con su propio .resx (MEF-ADR-0009).
    /// </summary>
    public static bool TryNormalizar(string valor, out string tipo)
    {
        tipo = valor.Trim().ToLowerInvariant();
        return tipo is Descanso or Extra;
    }
}
