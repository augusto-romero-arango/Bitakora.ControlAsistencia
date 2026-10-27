using System.Text.Json.Nodes;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoDeHorarioAbiertoFunction;

internal static class TurnoDeHorarioAbiertoEventos
{
    public static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000001233");

    public static TurnoCreado Creado() =>
        TurnoCreado.CrearDeHorarioAbierto(TurnoId, "Turno Abierto",
            [new DatosFranja(new TimeOnly(4, 0), new TimeOnly(10, 0), [], [])]);

    public static VentanaDeEntradaModificada Ventana() =>
        VentanaDeEntradaModificada.Crear(TurnoId, HorasYMinutos.Crear(14, 0));

    public static string QuitarClave(string json, string clave)
    {
        var raiz = JsonNode.Parse(json)!;
        Quitar(raiz, clave);
        return raiz.ToJsonString();
    }

    private static void Quitar(JsonNode? nodo, string clave)
    {
        switch (nodo)
        {
            case JsonObject o:
                o.Remove(clave);
                foreach (var hijo in o.Select(p => p.Value).ToList()) Quitar(hijo, clave);
                break;
            case JsonArray a:
                foreach (var hijo in a) Quitar(hijo, clave);
                break;
        }
    }
}
