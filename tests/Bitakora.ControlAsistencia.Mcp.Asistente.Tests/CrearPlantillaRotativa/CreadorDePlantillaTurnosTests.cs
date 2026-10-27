using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.CrearPlantillaRotativa;

public class CreadorDePlantillaTurnosTests
{
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaPlantillas = "/api/programacion/plantillas-turnos";
    private const string TurnoManana = "8f14e45f-ceea-4b3c-8f0a-000000000001";
    private const string TurnoInexistente = "8f14e45f-ceea-4b3c-8f0a-0000000000e1";

    private static readonly MensajesDeCreacion Mensajes = new(
        "rechazo:{0}", "faltan:{0}", new MensajesDeTurnoEnLinea("rechazo:{0}", "conflicto:{0}", "dup:{0}", "fallo:{0}", "ninguno"));

    private static string TurnosJson => Fixtures.Leer("SolicitarProgramacionTurno", "turnos.json");

    private static (CreadorDePlantillaTurnos Creador, HandlerPorRuta Handler) Crear(
        Func<string, HttpStatusCode>? statusPut = null)
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, TurnosJson);
        handler.RespondeDetalleDeTurno(TurnosJson, TurnoManana);
        handler.Responde(HttpMethod.Get, $"{RutaTurnos}/{TurnoInexistente}", HttpStatusCode.NotFound, "");
        handler.Responde(HttpMethod.Post, RutaPlantillas, HttpStatusCode.Created, "");
        handler.RespondeConPrefijo(
            HttpMethod.Put, $"{RutaPlantillas}/",
            (r, _) => new HttpResponseMessage(statusPut?.Invoke(r.RequestUri!.AbsolutePath) ?? HttpStatusCode.NoContent));
        var api = new ProgramacionApi(cliente);
        return (new CreadorDePlantillaTurnos(api, new ResolutorTurnoPorId(api), new ResolutorTurnoPorNombre(api)), handler);
    }

    [Fact]
    public async Task CrearPlantillaTurnos_HaceUnPostYUnPutPorDia_CuandoLosTurnosExisten()
    {
        var (creador, handler) = Crear();
        var asignaciones = new List<AsignacionDeDia> { new(1, TurnoManana, null), new(2, TurnoManana, null) };

        var resultado = await creador.CrearAsync("Ciclo", 2, asignaciones, null, Mensajes, TestContext.Current.CancellationToken);

        resultado.Rechazo.Should().BeNull();
        var plantilla = resultado.Plantilla!;
        plantilla.DiasAsignados.Should().Be(2);
        plantilla.DiasRechazados.Should().BeEmpty();
        plantilla.TurnoPorDia[1].Should().Be("Cocina Manana");
        var post = handler.Requests.Single(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaPlantillas);
        JsonNode.Parse(post.Cuerpo!)!["plantillaId"]!.GetValue<string>().Should().Be(plantilla.PlantillaId.ToString());
        handler.Requests.Count(r => r.Metodo == HttpMethod.Put).Should().Be(2);
    }

    [Fact]
    public async Task CrearPlantillaTurnos_RechazaSinEscribirYListaLosFaltantes_CuandoUnTurnoNoExiste()
    {
        var (creador, handler) = Crear();
        var asignaciones = new List<AsignacionDeDia> { new(1, TurnoManana, null), new(2, TurnoInexistente, null) };

        var resultado = await creador.CrearAsync("Ciclo", 2, asignaciones, null, Mensajes, TestContext.Current.CancellationToken);

        resultado.Plantilla.Should().BeNull();
        resultado.Rechazo.Should().Be($"faltan:{TurnoInexistente}");
        handler.Requests.Should().OnlyContain(r => r.Metodo == HttpMethod.Get);
    }

    [Fact]
    public async Task CrearPlantillaTurnos_ListaElDiaRechazadoYSigue_CuandoUnPutRespondeConflicto()
    {
        var (creador, handler) = Crear(ruta => ruta.EndsWith("/dias/1", StringComparison.Ordinal)
            ? HttpStatusCode.Conflict
            : HttpStatusCode.NoContent);
        var asignaciones = new List<AsignacionDeDia> { new(1, TurnoManana, null), new(2, TurnoManana, null) };

        var resultado = await creador.CrearAsync("Ciclo", 2, asignaciones, null, Mensajes, TestContext.Current.CancellationToken);

        var plantilla = resultado.Plantilla!;
        plantilla.DiasAsignados.Should().Be(1);
        plantilla.DiasRechazados.Should().ContainSingle().Which.Dia.Should().Be(1);
        handler.Requests.Count(r => r.Metodo == HttpMethod.Put).Should().Be(2);
    }
}
