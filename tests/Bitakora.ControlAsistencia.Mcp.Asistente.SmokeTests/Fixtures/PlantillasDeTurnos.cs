using System.Net.Http.Json;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

// Arrange directo al Function App de Programacion: las tools de crear plantilla de turnos nacen
// despues que las de consulta, asi que el smoke siembra la plantilla por la misma via que
// ProgramacionApiFixture usa para los turnos (MEF-ADR-0048 seccion 6).
public static class PlantillasDeTurnos
{
    public static async Task<Guid> CrearPlantillaAsync(
        this HttpClient programacion, string nombre, int diasDelCiclo, CancellationToken ct)
    {
        var plantillaId = Guid.CreateVersion7();
        var respuesta = await programacion.PostAsJsonAsync(
            "/api/programacion/plantillas-turnos", new { plantillaId, nombre, diasDelCiclo }, ct);
        respuesta.EnsureSuccessStatusCode();
        return plantillaId;
    }

    public static async Task AsignarTurnoADiaAsync(
        this HttpClient programacion, Guid plantillaId, int dia, Guid turnoId, CancellationToken ct)
    {
        var respuesta = await programacion.PutAsJsonAsync(
            $"/api/programacion/plantillas-turnos/{plantillaId}/dias/{dia}", new { turnoId }, ct);
        respuesta.EnsureSuccessStatusCode();
    }
}
