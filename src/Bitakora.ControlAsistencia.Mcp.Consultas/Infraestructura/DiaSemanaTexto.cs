namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Mapeo del numero ISO 8601 de un dia (1 = lunes .. 7 = domingo, DiaDelCuadro.Dia) al nombre en
/// espanol que arma el cuadro de obtener_plantilla_semanal. Helper propio de este servidor: no
/// comparte tipo con DiaSemanaMcp de Mcp.Comandos (islas, MEF-ADR-0047 decision 3).
/// </summary>
public static class DiaSemanaTexto
{
    private static readonly IReadOnlyDictionary<int, string> Nombres = new Dictionary<int, string>
    {
        [1] = "lunes",
        [2] = "martes",
        [3] = "miercoles",
        [4] = "jueves",
        [5] = "viernes",
        [6] = "sabado",
        [7] = "domingo",
    };

    public static string NombreDe(int numeroIso) => Nombres[numeroIso];
}
