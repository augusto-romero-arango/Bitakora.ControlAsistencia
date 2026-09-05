using System.Text.Json;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

// Forma minima de SedeProgramada para asertar sobre el JSON persistido sin referenciar
// Programacion.DomainEvents desde los smoke tests (mismo criterio que DeadLetterMinimos). Leerla
// de forma case-insensitive deja la politica de nombres del serializador fuera de la asercion --
// lo que se verifica es el DATO que quedo grabado, no como el host llama a la clave.
public sealed record SedeMinima(string Id, string Nombre, string? CentroDeCostos = null);

public static class LectorDeSede
{
    public static readonly JsonSerializerOptions OpcionesLectura = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static SedeMinima? SedeDe(JsonElement franja) =>
        franja.TryGetProperty("sede", out var sede)
            ? sede.Deserialize<SedeMinima>(OpcionesLectura)
            : null;
}
