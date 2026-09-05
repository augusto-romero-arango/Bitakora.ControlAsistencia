namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

// Traduce el dia de una entrada de plantilla semanal (lunes..domingo, sin distinguir mayusculas ni
// acentos, o 1..7) al numero ISO 8601 que el PUT AsignarTurnoADia espera en la ruta (1 = lunes,
// MEF-ADR-0043). #628 reutiliza este helper (MEF-ADR-0018: segunda aparicion del mismo parseo).
public static class DiaSemanaMcp
{
    private static readonly IReadOnlyDictionary<string, int> NombresIso = new Dictionary<string, int>
    {
        ["LUNES"] = 1,
        ["MARTES"] = 2,
        ["MIERCOLES"] = 3,
        ["JUEVES"] = 4,
        ["VIERNES"] = 5,
        ["SABADO"] = 6,
        ["DOMINGO"] = 7
    };

    /// <summary>
    /// numeroIso en 1..7 (1 = lunes) si valor es un nombre de dia (con o sin acento, cualquier
    /// capitalizacion) o un numero "1".."7"; false para cualquier otro texto, incluido "0" y "8".
    /// </summary>
    public static bool TryParsear(string valor, out int numeroIso)
    {
        throw new NotImplementedException();
    }
}
