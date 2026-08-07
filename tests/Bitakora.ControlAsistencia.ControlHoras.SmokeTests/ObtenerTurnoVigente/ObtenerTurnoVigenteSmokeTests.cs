using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.ObtenerTurnoVigente;

// Issue #328: smoke tests de ObtenerTurnoVigente, GET control-horas/turnos-vigentes/{empleadoId}/{fecha}.
// Function GET read-side sobre la proyeccion TurnoVigente (receta N1, MEF-ADR-0034/0035) --
// reemplazo del read model del issue #289, retirado por la contraccion del issue #323. Mismo
// mecanismo de siembra que uso la suite de aquel read model (#289): se publica
// ProgramacionTurnoDiarioSolicitada al bus interno; ControlHoras persiste TurnoDiarioAsignado y el
// worker de proyecciones materializa la vista de forma asincrona.
//
// Estos tests quedan ROJOS hasta que el deploy publique ObtenerTurnoVigente en dev: mientras la
// revision anterior siga corriendo, la ruta no existe y el host responde 404 a todo -- el caso 400
// falla y el caso 404 pasa por la razon equivocada -- mismo precedente que las suites de #289 y
// #290. El CI de PR no los ejecuta (solo corre *.Tests); su
// veredicto real se lee despues del deploy.
//
// Lifecycle Async (MEF-ADR-0034 seccion 3): el worker materializa TurnoVigente DESPUES de que
// ControlHoras persiste turno_diario_asignado. El caso de exito envuelve la consulta en
// Polling.WaitUntilAsync (timeout estandar 30s) -- unica excepcion documentada al "no usar Polling
// directo en tests": si el timeout se agota es un fallo real (worker no desplegado o proyeccion sin
// registrar en el named store), nunca un skip.
//
// Formas locales DESACOPLADAS del read model de produccion (Bitakora.ControlAsistencia.ReadModels.
// ControlHoras.TurnoVigente/Bloque/TipoBloque): el smoke test no referencia ReadModels (isla, MEF-
// ADR-0034 seccion 5) ni el Function App. TipoBloqueSmoke replica el orden de valores del enum de
// produccion porque STJ lo serializa como el entero subyacente (ComposicionServicios no registra
// JsonStringEnumConverter para las respuestas HTTP) -- si produccion reordenara TipoBloque, este
// test detectaria el cambio de contrato al fallar la comparacion.
//
// No se repite aqui el CA-2 ("el ultimo gana", reasignacion sobrescribe) ni el mapeo detallado de
// descansos/extras: esas reglas de negocio de la proyeccion ya las cubre el unit test de
// TurnoVigenteProjection (projection-test-writer). Este smoke test es black-box: solo verifica que
// el endpoint desplegado responde con la vista materializada.
public class ObtenerTurnoVigenteSmokeTests(ApiFixture api, ServiceBusFixture serviceBus)
{
    private readonly HttpClient _client = api.Client;

    private const string TopicEntrada = "programacion-turno-diario-solicitada";
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
        string EmpleadoId,
        string NombreCompleto,
        DateOnly Fecha,
        string NombreTurno,
        string HorarioResumido,
        IReadOnlyList<BloqueSmoke> Bloques);

    private static string Ruta(string empleadoId, DateOnly fecha) =>
        $"/api/control-horas/turnos-vigentes/{empleadoId}/{fecha:yyyy-MM-dd}";

    // Mismo formato que ControlDiarioAggregateRoot.ComputarStreamId, reconstruido localmente:
    // el smoke test no referencia el Function App (ControlHoras.Entities).
    private static string ComputarStreamId(string empleadoId, DateOnly fecha) =>
        $"{empleadoId}:{fecha:yyyy-MM-dd}";

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
        var empleadoId = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 9);

        var evento = new
        {
            SolicitudId = solicitudId,
            Empleado = new
            {
                EmpleadoId = empleadoId,
                TipoIdentificacion = "CC",
                NumeroIdentificacion = "111222333",
                Nombres = "[TEST] Smoke",
                Apellidos = "[TEST] TurnoVigente"
            },
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
        var ruta = Ruta(empleadoId, fecha);
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

        // Assert: Id como stream key -- ancla para comandos/lookups de la UI, no se pinta pero SI
        // viaja en la respuesta (decision de entrevista, "Notas tecnicas" del issue #328).
        respuesta.Id.Should().Be(ComputarStreamId(empleadoId, fecha));
        respuesta.EmpleadoId.Should().Be(empleadoId);
        respuesta.NombreCompleto.Should().Be("[TEST] Smoke [TEST] TurnoVigente");
        respuesta.Fecha.Should().Be(fecha);
        respuesta.NombreTurno.Should().Be("[TEST] Turno Vigente Query");
        respuesta.HorarioResumido.Should().Be("[TEST] Turno Vigente 08:00-16:00");

        // Assert: Bloques -- un solo tramo Ordinaria (sin descansos/extras/cruce de medianoche),
        // absoluto contra la fecha de asignacion (TurnoDiario.Segmentar, issue #327).
        var bloqueEsperado = new BloqueSmoke(
            TipoBloqueSmoke.Ordinaria,
            fecha.ToDateTime(new TimeOnly(8, 0)),
            fecha.ToDateTime(new TimeOnly(16, 0)));
        respuesta.Bloques.Should().BeEquivalentTo([bloqueEsperado]);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerTurnoVigente_Retorna404_CuandoNoHayTurnoVigenteParaEseEmpleadoYFecha()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: empleadoId nuevo, nunca creado por ningun test -- no puede tener turno vigente.
        var empleadoId = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 10);

        var response = await _client.GetAsync(Ruta(empleadoId, fecha), ct);

        // Assert: CA-4 -- 404 SIN BODY (mismo criterio que #289): distingue el
        // NotFoundResult() del endpoint de un 404 con payload de error, y de la pagina de error del
        // host si la ruta no existiera.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerTurnoVigente_Retorna400_CuandoLaFechaTieneFormatoInvalido()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: formato DD-MM-YYYY en vez de yyyy-MM-dd (mismo criterio que #289).
        var empleadoId = Guid.CreateVersion7().ToString();

        var response = await _client.GetAsync(
            $"/api/control-horas/turnos-vigentes/{empleadoId}/09-04-2026", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
