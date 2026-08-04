using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.PublicEvents.Empleados;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.ObtenerTurnoDiario;

// Issue #289: smoke tests de ObtenerTurnoDiario, GET control-horas/turnos-diarios/{empleadoId}/{fecha}.
// Es la primera Function GET read-side del BC (skills/projections, MEF-ADR-0034/0035): el "comando"
// que origina la vista no llega por HTTP -- ControlHoras la recibe via ProgramacionTurnoDiarioSolicitada
// en el bus interno (precedente: AsignarTurnoViaSbSmokeTests, mismo patron de inyeccion por bus).
//
// La proyeccion TurnoDiarioView tiene lifecycle Async: un worker aparte (Container App, daemon Marten)
// la materializa DESPUES de que ControlHoras persiste turno_diario_asignado. El GET inmediato puede
// devolver 404 legitimamente, asi que el caso de exito envuelve la consulta en Polling.WaitUntilAsync
// (timeout estandar 30s) -- unica excepcion documentada al "no usar Polling directo en tests": si el
// timeout se agota es un fallo real (worker no desplegado o proyeccion sin registrar), nunca un skip.
//
// TurnoDiarioRespuestaSmoke es una forma local, DESACOPLADA del DTO de produccion
// (Bitakora.ControlAsistencia.ControlHoras.ObtenerTurnoDiario.TurnoDiarioRespuesta): el smoke test no
// referencia proyectos de dominio. Reutiliza InformacionEmpleado y DetalleTurno de
// PublicEvents/PrivateEvents (ya referenciados por este proyecto, mismo patron que
// AsignarTurnoViaSbSmokeTests) porque son la representacion portable compartida entre el evento, la
// vista y la respuesta HTTP.
public class ObtenerTurnoDiarioSmokeTests(ApiFixture api, ServiceBusFixture serviceBus)
{
    private readonly HttpClient _client = api.Client;

    private const string TopicEntrada = "programacion-turno-diario-solicitada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Case-insensitive: la respuesta viaja en camelCase (ComposicionServicios configura
    // JsonNamingPolicy.CamelCase para las respuestas HTTP), mientras que las formas locales de este
    // archivo son PascalCase (mismo patron que ServiceBusFixture, que enfrenta la misma asimetria).
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record TurnoDiarioRespuestaSmoke(
        InformacionEmpleado Empleado,
        DateOnly Fecha,
        DetalleTurno DetalleTurno,
        Guid UltimaSolicitudId);

    private static string Ruta(string empleadoId, DateOnly fecha) =>
        $"/api/control-horas/turnos-diarios/{empleadoId}/{fecha:yyyy-MM-dd}";

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
    public async Task ObtenerTurnoDiario_Retorna200ConElTurnoVigente_CuandoElBusAsignaElTurno()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        // Arrange: identificadores unicos por ejecucion y fecha fija (nunca DateTime.UtcNow).
        var solicitudId = Guid.CreateVersion7();
        var empleadoId = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 5, 4);

        // Issue #288: en produccion la Descripcion la calcula Programacion (CatalogoTurnos/
        // FranjaOrdinaria.ToString()). Aqui se controla el valor exacto en el evento sintetico para
        // verificar, sin repetir esa logica, que la proyeccion y el mapeo a respuesta la propagan
        // literal (CA-8: "la descripcion que aporta #288").
        const string descripcionTurno = "[TEST] Turno Diario 08:00-16:00";
        const string descripcionFranja = "[TEST] Franja 08:00-16:00";

        var evento = new
        {
            SolicitudId = solicitudId,
            Empleado = new
            {
                EmpleadoId = empleadoId,
                TipoIdentificacion = "CC",
                NumeroIdentificacion = "444555666",
                Nombres = "[TEST] Smoke Query",
                Apellidos = "[TEST] ObtenerTurnoDiario"
            },
            Fecha = fecha.ToString("yyyy-MM-dd"),
            DetalleTurno = new
            {
                Nombre = "[TEST] Turno Query HU289",
                FranjasOrdinarias = new[]
                {
                    new
                    {
                        HoraInicio = "08:00:00",
                        HoraFin = "16:00:00",
                        DiaOffsetFin = 0,
                        Descansos = Array.Empty<object>(),
                        Extras = Array.Empty<object>(),
                        Descripcion = descripcionFranja
                    }
                },
                Descripcion = descripcionTurno
            }
        };

        // Act: publicar al bus interno -- ControlHoras persiste TurnoDiarioAsignado y el worker de
        // proyecciones materializa TurnoDiarioView de forma asincrona.
        await serviceBus.PublishAsync(TopicEntrada, evento, solicitudId.ToString());

        // Act + Assert: reintentar el GET hasta que la proyeccion asincrona materialice la vista.
        var ruta = Ruta(empleadoId, fecha);
        var respuesta = await Polling.WaitUntilAsync(async () =>
        {
            var response = await _client.GetAsync(ruta, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await response.Content.ReadFromJsonAsync<TurnoDiarioRespuestaSmoke>(
                JsonOptions, cancellationToken: ct);
        }, Timeout);

        respuesta.Should().NotBeNull(
            "el worker de proyecciones deberia materializar TurnoDiarioView dentro del timeout");

        // Assert: empleado y fecha (CA-8).
        respuesta!.Fecha.Should().Be(fecha);
        respuesta.UltimaSolicitudId.Should().Be(solicitudId);

        var empleadoEsperado = new InformacionEmpleado(
            empleadoId, "CC", "444555666", "[TEST] Smoke Query", "[TEST] ObtenerTurnoDiario");
        respuesta.Empleado.Should().Be(empleadoEsperado);

        // Assert: nombre del turno y descripcion (#288), a nivel de turno y de franja anidada.
        respuesta.DetalleTurno.Nombre.Should().Be("[TEST] Turno Query HU289");
        respuesta.DetalleTurno.Descripcion.Should().Be(descripcionTurno);
        respuesta.DetalleTurno.FranjasOrdinarias.Should().HaveCount(1);
        respuesta.DetalleTurno.FranjasOrdinarias[0].Descripcion.Should().Be(descripcionFranja);

        // Assert: estructura completa de franjas anidadas (CA-8). BeEquivalentTo compara
        // propiedad-por-propiedad (incluida Descripcion), a diferencia del Equals de DetalleTurno/
        // DetalleFranjaOrdinaria que la excluye a proposito (issue #288, identidad vs. dato derivado).
        var detalleTurnoEsperado = new DetalleTurno("[TEST] Turno Query HU289", [
            new DetalleFranjaOrdinaria(
                new TimeOnly(8, 0), new TimeOnly(16, 0), 0,
                Array.Empty<DetalleSubFranja>(), Array.Empty<DetalleSubFranja>(), descripcionFranja)
        ], descripcionTurno);
        respuesta.DetalleTurno.Should().BeEquivalentTo(detalleTurnoEsperado);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerTurnoDiario_Retorna404_CuandoNoHayTurnoVigenteParaEseEmpleadoYFecha()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: empleadoId nuevo, nunca creado por ningun test -- no puede tener turno vigente.
        var empleadoId = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 5, 5);

        // Act
        var response = await _client.GetAsync(Ruta(empleadoId, fecha), ct);

        // Assert: CA-6 -- 404 sin body, respuesta correcta (no error) cuando no hay turno asignado.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerTurnoDiario_Retorna400_CuandoLaFechaTieneFormatoInvalido()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: formato DD-MM-YYYY en vez de yyyy-MM-dd (nota tecnica del issue #289).
        var empleadoId = Guid.CreateVersion7().ToString();

        // Act
        var response = await _client.GetAsync(
            $"/api/control-horas/turnos-diarios/{empleadoId}/31-12-2026", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
