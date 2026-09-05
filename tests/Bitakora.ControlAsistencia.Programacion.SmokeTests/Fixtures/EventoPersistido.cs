using System.Text.Json;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

// Lectura del JSON que Marten grabo en mt_events. Hacerla case-insensitive deja la politica de
// nombres del serializador del host fuera de la asercion -- lo que se verifica es el DATO que
// quedo grabado, no como el host llama a la clave.
public static class EventoPersistido
{
    public static readonly JsonSerializerOptions OpcionesLectura = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Devuelve null cuando la franja no trae sede: la clave se omite del JSON (ShouldSerialize),
    // asi que su ausencia es un dato valido y no un error de lectura.
    public static SedeMinima? SedeDe(JsonElement franja) =>
        franja.TryGetProperty("sede", out var sede)
            ? sede.Deserialize<SedeMinima>(OpcionesLectura)
            : null;
}
