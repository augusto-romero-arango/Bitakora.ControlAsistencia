// Issue #465 (MEF-ADR-0043 paso 2): smoke tests del endpoint PUT colaboradores/{id}/sede --
// reemplazo completo del VO atomico "sede del colaborador" (referencia pura al maestro de Sedes,
// solo CodigoSede -- CA-ADR-0029, islas: el servidor NUNCA consulta el maestro). Molde:
// AsignarEtiquetaSmokeTests (#376) -- mismo comando event-sourcing puro sin consumidores downstream
// (CA-ADR-0030): sin ServiceBusFixture, la unica verificacion black-box de los efectos del handler
// es leer mt_events via PostgresFixture. Aqui no hay equivalente a {categoria}: el codigo de sede
// viaja unicamente en el body.
//
// Arrange: AsignarSede exige un ColaboradorAggregateRoot existente -- el arrange de cada test
// registra el colaborador y, cuando aplica, termina su vinculacion o inicia una vinculacion nueva
// (reingreso) via los mismos comandos que los originan, nunca sembrando datos por fuera del API.
//
// CA-1 (ruta de exito): 202 + el stream recibe sede_asignada con el CodigoSede del comando.
// CA-2 (ruta de exito): reasignar una sede DISTINTA a la vigente agrega un evento nuevo (conteo
// pasa de 1 a 2) -- reemplazo puro, sin evento de retiro.
// CA-3 (ruta de exito): asignar la MISMA sede vigente (comparacion exacta, case-sensitive) no
// agrega evento nuevo -- idempotencia silenciosa, el conteo se mantiene en 1.
// CA-4 (rutas de rechazo): la ultima vinculacion tiene terminacion registrada -- pasada o un
// preaviso cuya fecha no ha llegado, sin distincion -> 409, sin evento.
// CA-5 (ruta de exito, reingreso nace sin sede): tras un reingreso, asignar la MISMA sede que tenia
// la vinculacion anterior SI agrega un evento nuevo (conteo pasa de 1 a 2) -- prueba indirecta de
// que el estado se limpio (si no se hubiera limpiado, la comparacion exacta habria disparado
// SinCambios y el conteo se habria quedado en 1). Este dominio no expone todavia una vista de la
// sede vigente (#519, hermano read-side): el efecto solo es observable black-box a traves de este
// mecanismo de idempotencia.
// CA-6: colaborador inexistente -> 404.
// Body invalido (CodigoSede vacio) -> 400 via AsignarSedeBodyValidator.
// {id} de ruta invalido -> 400 (guarda compartida IdentificacionDeRuta, ya cubierta exhaustivamente
// por AsignarEtiquetaSmokeTests y otros smoke tests del dominio; aqui solo un caso de sanity para
// confirmar que este endpoint tambien la invoca).
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.AsignarSedeFunction;

public class AsignarSedeSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/colaboradores";
    private const string SchemaColaboradores = "colaboradores";
    private const string TipoEventoSedeAsignada = "sede_asignada";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Segunda lectura del mismo evento que ExisteEventoAsync ya espero: si el primer polling
    // termino, el evento esta -- no hay nada mas que esperar.
    private static readonly TimeSpan TimeoutLecturaConfirmada = TimeSpan.FromSeconds(5);

    // Numero unico por test -- evita colisiones entre ejecuciones repetidas del smoke test: la
    // identidad del stream es Identificacion.ToString() ("CC-<numero>"), no un Guid nuevo por
    // llamada.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Oraculo independiente de la clave de stream (MEF-ADR-0002): se recompone aqui a mano, no se
    // deriva de Identificacion.ToString(), para que un cambio de formato en el VO no se auto-valide.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    private static string RutaSede(string id) => $"/api/colaboradores/{id}/sede";

    private static object PayloadRegistro(string numeroIdentificacion, DateOnly fechaInicio, string codigoColaborador) => new
    {
        tipoIdentificacion = TipoIdentificacionCc,
        numeroIdentificacion,
        primerNombre = "[TEST]",
        segundoNombre = (string?)null,
        primerApellido = "Smoke",
        segundoApellido = (string?)null,
        codigoColaborador,
        fechaInicio
    };

    private static object PayloadIniciarVinculacion(string codigoColaborador, DateOnly fechaInicio) => new
    {
        codigoColaborador,
        fechaInicio
    };

    private static object PayloadCodigoSede(string codigoSede) => new { codigoSede };

    // Arrange comun: registra un colaborador con una vinculacion abierta -- via el comando que la
    // origina, nunca sembrando el event store por fuera del API. Devuelve el codigo de la
    // vinculacion inicial para que el arrange lo use como {codigo} de ruta al terminar.
    private async Task<string> RegistrarColaboradorAsync(
        string numeroIdentificacion, DateOnly fechaInicio, CancellationToken ct)
    {
        var codigo = NuevoCodigoColaborador();

        var response = await _client.PostAsJsonAsync(
            RutaRegistrar, PayloadRegistro(numeroIdentificacion, fechaInicio, codigo), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que RegistrarColaborador funcione");

        return codigo;
    }

    // Arrange comun (CA-4): cierra la vinculacion vigente -- via el comando que la origina, nunca
    // sembrando el event store por fuera del API.
    private async Task TerminarVinculacionAsync(
        string id, string codigo, DateOnly fechaEfectiva, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/colaboradores/{id}/vinculaciones/{codigo}:terminar",
            new { fechaEfectiva },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que TerminarVinculacion funcione");
    }

    // Arrange comun (CA-5): inicia una vinculacion nueva sobre el colaborador tras una terminacion
    // -- escenario de negocio de reingreso -- via el comando que lo origina, nunca sembrando el
    // event store por fuera del API.
    private async Task IniciarVinculacionAsync(
        string numeroIdentificacion, DateOnly fechaInicio, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/colaboradores/{ComputarStreamId(numeroIdentificacion)}/vinculaciones",
            PayloadIniciarVinculacion(NuevoCodigoColaborador(), fechaInicio),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que IniciarVinculacion funcione");
    }

    private Task<HttpResponseMessage> AsignarSedeAsync(string id, string codigoSede, CancellationToken ct) =>
        _client.PutAsJsonAsync(RutaSede(id), PayloadCodigoSede(codigoSede), ct);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-1: camino feliz -- colaborador con vinculacion abierta y sin sede -> 202 y el stream
    // recibe sede_asignada con el codigo enviado. Sin Service Bus (event-sourcing puro): mt_events
    // es la unica ventana black-box a lo que quedo grabado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSede_Retorna202YPersisteSedeAsignada_CuandoColaboradorNoTieneSede()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 15), ct);

        var response = await AsignarSedeAsync(id, "BOG", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaColaboradores, id, TipoEventoSedeAsignada, Timeout);

        eventoPersistido.GetProperty("CodigoSede").GetString().Should().Be("BOG");
    }

    // CA-2: reasignar una sede DISTINTA a la vigente agrega un evento nuevo -- reemplazo puro, sin
    // evento de retiro. El conteo pasa de 1 a 2 y el evento nuevo lleva el codigo reasignado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSede_Retorna202YAgregaOtroEvento_CuandoSedeEsDistintaALaVigente()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 20), ct);

        var primeraAsignacion = await AsignarSedeAsync(id, "BOG", ct);
        primeraAsignacion.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la primera asignacion funcione");

        var existePrimeraSede = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoSedeAsignada, Timeout);
        existePrimeraSede.Should().BeTrue(
            $"el evento {TipoEventoSedeAsignada} de la primera asignacion deberia estar en el stream {id}");

        var segundaAsignacion = await AsignarSedeAsync(id, "MED", ct);

        segundaAsignacion.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, id, TipoEventoSedeAsignada);

        asignaciones.Should().Be(2,
            "reasignar una sede distinta a la vigente deberia agregar un evento nuevo (reemplazo puro, sin retiro)");

        var eventoConSedeNueva = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaColaboradores, id, TipoEventoSedeAsignada, "CodigoSede", "MED", TimeoutLecturaConfirmada);

        eventoConSedeNueva.GetProperty("CodigoSede").GetString().Should().Be("MED");
    }

    // CA-3: el codigo del comando es IGUAL (comparacion exacta, case-sensitive) al ya asignado ->
    // idempotencia silenciosa: ningun evento nuevo, el conteo se mantiene en 1.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSede_Retorna202SinNuevoEvento_CuandoSedeEsIdenticaALaVigente()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 25), ct);

        var primeraAsignacion = await AsignarSedeAsync(id, "BOG", ct);
        primeraAsignacion.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la primera asignacion funcione");

        var existePrimeraSede = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoSedeAsignada, Timeout);
        existePrimeraSede.Should().BeTrue(
            $"el evento {TipoEventoSedeAsignada} de la primera asignacion deberia estar en el stream {id}");

        var segundaAsignacion = await AsignarSedeAsync(id, "BOG", ct);

        segundaAsignacion.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, id, TipoEventoSedeAsignada);

        asignaciones.Should().Be(1,
            "asignar la misma sede ya vigente no deberia persistir un evento nuevo (idempotencia silenciosa)");
    }

    // CA-4 (regla de apertura estricta): la ULTIMA vinculacion tiene terminacion registrada -> 409,
    // sin evento nuevo (CA-ADR-0030; MEF-ADR-0043 seccion 2 paso 2: el 409 de un PUT es una
    // instancia mas de "declinar con resultado", RFC 9110 §9.3.4).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSede_Retorna409_CuandoUltimaVinculacionTieneTerminacionRegistrada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 1), ct);
        await TerminarVinculacionAsync(
            ComputarStreamId(numeroIdentificacion), codigo, new DateOnly(2026, 5, 1), ct);

        var response = await AsignarSedeAsync(id, "BOG", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-4 (preaviso no vencido): un preaviso con fecha futura ya registrado bloquea igual -- la
    // sede describe la relacion laboral ACTIVA, sin importar si la fecha efectiva ya paso.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSede_Retorna409_CuandoTerminacionEsUnPreavisoConFechaFutura()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);
        var fechaPreavisoFutura = new DateOnly(2030, 12, 31);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 1), ct);
        await TerminarVinculacionAsync(ComputarStreamId(numeroIdentificacion), codigo, fechaPreavisoFutura, ct);

        var response = await AsignarSedeAsync(id, "BOG", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-5 (reingreso nace sin sede): tras un reingreso, asignar la MISMA sede que tenia la
    // vinculacion anterior SI agrega un evento nuevo -- si el estado no se hubiera limpiado, la
    // comparacion exacta de CA-3 habria disparado SinCambios y el conteo se habria quedado en 1.
    // Este dominio no expone todavia una vista de la sede vigente (#519): esta es la unica forma
    // black-box de observar que el reingreso limpio el estado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSede_Retorna202YAgregaOtroEvento_CuandoVinculacionEsUnReingresoConLaMismaSedeQueTeniaAntesDeTerminar()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 10), ct);

        var asignacionPrevia = await AsignarSedeAsync(id, "BOG", ct);
        asignacionPrevia.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la asignacion previa al reingreso funcione");

        await TerminarVinculacionAsync(
            ComputarStreamId(numeroIdentificacion), codigo, new DateOnly(2026, 6, 1), ct);
        await IniciarVinculacionAsync(numeroIdentificacion, new DateOnly(2026, 7, 1), ct);

        var response = await AsignarSedeAsync(id, "BOG", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, id, TipoEventoSedeAsignada);

        asignaciones.Should().Be(2,
            "el reingreso deberia dejar al colaborador sin sede: asignar el mismo codigo de antes de terminar no deberia tratarse como SinCambios");
    }

    // CA-6: colaborador inexistente -> 404, sin escribir nada al event store (no hay stream para
    // consultar: la ausencia de escritura la garantiza el propio 404 -- el handler lanza antes de
    // llegar al aggregate).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSede_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion(); // nunca registrado
        var id = ComputarStreamId(numeroIdentificacion);

        var response = await AsignarSedeAsync(id, "BOG", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Body invalido: CodigoSede vacio -> 400 via AsignarSedeBodyValidator.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSede_Retorna400_CuandoCodigoSedeDelBodyEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = ComputarStreamId(NuevoNumeroIdentificacion());

        var response = await AsignarSedeAsync(id, codigoSede: "", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // {id} de ruta sin guion -> 400. Guarda compartida (IdentificacionDeRuta), ya cubierta
    // exhaustivamente por AsignarEtiquetaSmokeTests -- este es solo un caso de sanity para
    // confirmar que este endpoint tambien la invoca.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSede_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var ct = TestContext.Current.CancellationToken;
        var idSinGuion = NuevoNumeroIdentificacion(); // p.ej. "3F2A0C..." sin "CC-" adelante

        var response = await AsignarSedeAsync(idSinGuion, "BOG", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
