namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Mapeo del numero ISO 8601 de un dia (1 = lunes .. 7 = domingo, DiaDelCuadro.Dia) al nombre en
/// espanol que arma el cuadro de obtener_plantilla_semanal. Helper propio de este servidor: no
/// comparte tipo con DiaSemanaMcp de Mcp.Comandos (islas, MEF-ADR-0047 decision 3).
/// </summary>
public static class DiaSemanaTexto
{
    public static string NombreDe(int numeroIso) => throw new NotImplementedException();
}
