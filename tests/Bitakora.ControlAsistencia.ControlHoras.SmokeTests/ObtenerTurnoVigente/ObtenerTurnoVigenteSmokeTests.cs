using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.ObtenerTurnoVigente;

// El arrange va por el bus interno (ProgramacionTurnoDiarioSolicitada al topic de entrada), nunca
// sembrando el event store por fuera de el. El CI de PR no ejecuta esta suite (solo corre *.Tests);
// su veredicto real se lee tras el deploy.
//
// Lifecycle Async (MEF-ADR-0034 seccion 3): el worker materializa TurnoVigente DESPUES de que
// ControlHoras persiste turno_diario_asignado, asi que el caso de exito envuelve la consulta en
// Polling.WaitUntilAsync -- unica excepcion al "no usar Polling directo en tests": agotar el
// timeout es un fallo real (worker no desplegado o proyeccion sin registrar en el named store),
// nunca un skip.
//
// Formas locales DESACOPLADAS del read model de produccion: el smoke test no referencia ReadModels
// (isla, MEF-ADR-0034 seccion 5) ni el Function App. TipoBloqueSmoke replica el ORDEN de valores
// del enum de produccion porque STJ lo serializa como el entero subyacente (ComposicionServicios no
// registra JsonStringEnumConverter para las respuestas HTTP) -- si produccion reordenara TipoBloque,
// la comparacion falla y delata el cambio de contrato.
//
// Las reglas de negocio de la proyeccion (reasignacion, mapeo de descansos/extras) las cubre el
// unit test de TurnoVigenteProjection; aqui solo interesa que el endpoint desplegado responda con
// la vista materializada.
public class ObtenerTurnoVigenteSmokeTests(ApiFixture api, ServiceBusFixture serviceBus)
{
    private readonly HttpClient _client = api.Client;

    private const string TopicEntrada = "programacion-turno-diario-solicitada";

    // Eco del colaborador sembrado: la vista devuelve NombreCompleto tal cual viaja en el evento.
    private const string NombreCompletoSembrado = "[TEST] Smoke TurnoVigente";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Case-insensitive: la respuesta viaja en camelCase (ComposicionServicios configura
    // JsonNamingPolicy.CamelCase para las respuestas HTTP), mientras que las formas locales de este
    // archivo son PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private enum TipoBloqueSmoke
    {
        Ordinaria,
        Descanso,
        Extra
    }

    private sealed record BloqueSmoke(TipoBloqueSmoke Tipo, DateTime Inicio, DateTime Fin);

    private sealed record TurnoVigenteRespuestaSmoke(
        string Id,
        string CodigoColaborador,
        string NombreCompleto,
        DateOnly Fecha,
        string NombreTurno,
        string HorarioResumido,
        IReadOnlyList<BloqueSmoke> Bloques);

    private static string Ruta(string codigoColaborador, DateOnly fecha) =>
        $"/api/control-horas/turnos-vigentes/{codigoColaborador}/{fecha:yyyy-MM-dd}";

    // Mismo formato que ControlDiarioAggregateRoot.ComputarStreamId, reconstruido localmente:
    // el smoke test no referencia el Function App (ControlHoras.Entities).
    private static string ComputarStreamId(string codigoColaborador, DateOnly fecha) =>
        $"cd:{codigoColaborador}:{fecha:yyyyMMdd}";

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerTurnoVigente_Retorna200ConLaVistaCompleta_CuandoElBusAsignaElTurno()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        // Arrange: identificadores unicos por ejecucion y fecha fija (nunca DateTime.UtcNow).
        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 9);

        var evento = new
        {
            SolicitudId = solicitudId,
            Colaborador = new ResumenColaborador(
                "CC-111222333", codigoColaborador, NombreCompletoSembrado),
            Fecha = fecha.ToString("yyyy-MM-dd"),
            DetalleTurno = new
            {
                Nombre = "[TEST] Turno Vigente Query",
                FranjasOrdinarias = new[]
                {
                    new
                    {
                        HoraInicio = "08:00:00",
                        HoraFin = "16:00:00",
                        DiaOffsetFin = 0,
                        Descansos = Array.Empty<object>(),
                        Extras = Array.Empty<object>(),
                        Descripcion = (string?)null
                    }
                },
                Descripcion = "[TEST] Turno Vigente 08:00-16:00"
            }
        };

        // Act: publicar al bus interno -- ControlHoras persiste TurnoDiarioAsignado y el worker de
        // proyecciones materializa TurnoVigente de forma asincrona.
        await serviceBus.PublishAsync(TopicEntrada, evento, solicitudId.ToString());

        // Act + Assert: reintentar el GET hasta que la proyeccion asincrona materialice la vista.
        var ruta = Ruta(codigoColaborador, fecha);
        var respuesta = await Polling.WaitUntilAsync(async () =>
        {
            var response = await _client.GetAsync(ruta, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await response.Content.ReadFromJsonAsync<TurnoVigenteRespuestaSmoke>(
                JsonOptions, cancellationToken: ct);
        }, Timeout);

        // Sin assert de NotBeNull: WaitUntilAsync devuelve un valor no nulo o lanza TimeoutException
        // ("el worker no materializo TurnoVigente dentro del timeout"), nunca null.

        // Id como stream key: ancla para comandos/lookups de la UI, no se pinta pero SI viaja en
        // la respuesta.
        respuesta.Id.Should().Be(ComputarStreamId(codigoColaborador, fecha));
        respuesta.CodigoColaborador.Should().Be(codigoColaborador);
        respuesta.NombreCompleto.Should().Be(NombreCompletoSembrado);
        respuesta.Fecha.Should().Be(fecha);
        respuesta.NombreTurno.Should().Be("[TEST] Turno Vigente Query");
        respuesta.HorarioResumido.Should().Be("[TEST] Turno Vigente 08:00-16:00");

        // Bloques: un solo tramo Ordinaria (sin descansos/extras/cruce de medianoche), absoluto
        // contra la fecha de asignacion (TurnoDiario.Segmentar).
        var bloqueEsperado = new BloqueSmoke(
            TipoBloqueSmoke.Ordinaria,
            fecha.ToDateTime(new TimeOnly(8, 0)),
            fecha.ToDateTime(new TimeOnly(16, 0)));
        respuesta.Bloques.Should().BeEquivalentTo([bloqueEsperado]);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerTurnoVigente_Retorna404_CuandoNoHayTurnoVigenteParaEseColaboradorYFecha()
    {
        var ct = TestContext.Current.CancellationToken;

        // codigoColaborador nuevo, nunca creado por ningun test -- no puede tener turno vigente.
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 10);

        var response = await _client.GetAsync(Ruta(codigoColaborador, fecha), ct);

        // 404 SIN BODY: distingue el NotFoundResult() del endpoint de un 404 con payload de error,
        // y de la pagina de error del host si la ruta no existiera.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerTurnoVigente_Retorna400_CuandoLaFechaTieneFormatoInvalido()
    {
        var ct = TestContext.Current.CancellationToken;

        // Formato DD-MM-YYYY en vez de yyyy-MM-dd.
        var codigoColaborador = Guid.CreateVersion7().ToString();

        var response = await _client.GetAsync(
            $"/api/control-horas/turnos-vigentes/{codigoColaborador}/09-04-2026", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
