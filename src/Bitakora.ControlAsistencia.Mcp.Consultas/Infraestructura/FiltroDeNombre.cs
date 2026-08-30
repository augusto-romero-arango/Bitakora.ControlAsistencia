using System.Globalization;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Comparacion de nombres para los filtros de las tools de listado: ignora mayusculas y acentos
/// ("manana" encuentra "Mañana") porque quien escribe es un asistente conversando en español, no
/// un cliente que copia valores exactos.
/// </summary>
public static class FiltroDeNombre
{
    private static readonly CompareInfo Comparador = CultureInfo.InvariantCulture.CompareInfo;

    public static bool Contiene(string nombre, string filtro) =>
        Comparador.IndexOf(nombre, filtro, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
}
