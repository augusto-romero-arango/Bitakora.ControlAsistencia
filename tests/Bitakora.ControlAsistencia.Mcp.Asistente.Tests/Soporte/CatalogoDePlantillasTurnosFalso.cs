namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

public static class CatalogoDePlantillasTurnosFalso
{
    public const string RutaCatalogo = "/api/programacion/plantillas-turnos";

    public static string RutaDetalle(string plantillaId) => $"{RutaCatalogo}/{plantillaId}";

    public static string Turno(string nombre, bool completo = true, bool retirado = false, string descripcion = "(06:00-14:00)",
        bool esDescanso = false, string? duracionHorarioAbierto = null)
    {
        var horarioAbierto = duracionHorarioAbierto is null ? "null" : $$"""{"duracion":"{{duracionHorarioAbierto}}"}""";
        return $$"""{"id":"t-{{nombre}}","nombre":"{{nombre}}","descripcion":"{{descripcion}}","completo":{{completo.ToString().ToLowerInvariant()}},"retirado":{{retirado.ToString().ToLowerInvariant()}},"esDescanso":{{esDescanso.ToString().ToLowerInvariant()}},"horarioAbierto":{{horarioAbierto}}}""";
    }

    public static string Dia(int dia, string turnoJson) => $$"""{"dia":{{dia}},"turno":{{turnoJson}}}""";

    public static string Plantilla(
        string id, string nombre, int diasDelCiclo, bool completa, string[] dias, bool conJornada = true)
    {
        var cuerpo = $$"""{"id":"{{id}}","nombre":"{{nombre}}","diasDelCiclo":{{diasDelCiclo}},"completa":{{completa.ToString().ToLowerInvariant()}},"dias":[{{string.Join(",", dias)}}]""";
        cuerpo += conJornada ? ""","jornadaId":"j1"}""" : "}";
        return cuerpo;
    }

    public static string Sobre(string[] plantillas, string? siguienteCursor = null) =>
        $$"""{"elementos":[{{string.Join(",", plantillas)}}],"siguienteCursor":{{(siguienteCursor is null ? "null" : $"\"{siguienteCursor}\"")}}}""";
}
