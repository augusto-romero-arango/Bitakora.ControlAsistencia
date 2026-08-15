// Issue #351: smoke tests del endpoint POST Colaboradores/Nombres (corregir los nombres de un
// colaborador existente). Cuarto comando del ciclo de vida de ColaboradorAggregateRoot y el mas
// simple: sin reglas de estado (CA-ADR-0030) -- por eso este archivo no tiene caso 409. Molde:
// TerminarVinculacionSmokeTests/ReingresarColaboradorSmokeTests (#349/#350) -- mismo comando
// event-sourcing puro sin consumidores downstream: sin ServiceBusFixture, la unica verificacion
// black-box de los efectos del handler es leer mt_events via PostgresFixture.
//
// Arrange: CorregirNombres exige un ColaboradorAggregateRoot existente -- el arrange de cada test
// registra el colaborador (y, cuando aplica, termina su vinculacion) via los mismos comandos que
// los originan (#330 y #349), nunca sembrando datos por fuera del API.
//
// Contenido persistido (Nombre, un VO con ctor privado): se verifica deserializando el campo con
// la SERIALIZACION REAL de produccion -- NombreColaborador +
// ConfiguracionSerializacionColaboradores.CrearOpcionesMarten(), referenciadas desde
// Colaboradores.DomainEvents (ya cableado en el .csproj por el domain-scaffolder). Mismo criterio
// que AsignarTurnoViaSbSmokeTests (ControlHoras, issue #322): "el smoke test deserializa/serializa
// con el tipo que realmente posee el payload persistido", no con un tipo de bus --
// NombreColaborador ES ese tipo (MEF-ADR-0039 decision 6). La comparacion es por igualdad de valor
// (NombreColaborador.Equals, #348), NUNCA contra el texto JSON persistido: mt_events.data es jsonb
// y PostgreSQL no preserva whitespace ni orden de claves (docs 8.14.1).
//
// CA-1/CA-2 (rutas de exito): 202 + el stream recibe NombresCorregidos con el Nombre corregido --
// ya sea con la vinculacion abierta (CA-1) o con la ultima vinculacion TERMINADA (CA-2, prueba que
// la correccion solo exige existencia del colaborador, nunca vigencia de su vinculacion).
// CA-3: nombre igual por valor al actual -> 202 sin evento nuevo en el stream (idempotencia
// silenciosa, mecanismo "declinar en silencio" -- precedente ControlDiarioAggregateRoot.
// AdicionarMarcacion). La ausencia se verifica con un timeout corto (3s, no el estandar de 30s):
// este comando es event-sourcing puro sin proyeccion asincrona downstream -- si el evento no llego
// ya en la respuesta HTTP, nunca va a llegar; esperar el timeout estandar solo alargaria la
// suite sin ganar senal.
// CA-4: colaborador inexistente -> 404, sin escribir nada al event store.
// CA-5: request invalida (sin primer nombre, sin primer apellido, sin identificacion, tipo fuera
// de la lista) -> 400, sin tocar el event store.
// CA-6 (NombresCorregidos registrado en TiposPersistidos + round-trip de serializacion): cubierto
// transitivamente por CA-1/CA-2 contra el entorno real desplegado; el detalle exhaustivo del
// round-trip vive en NombresCorregidosSerializacionTests.cs (*.Tests), no se duplica aqui.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.CorregirNombresFunction;

public class CorregirNombresSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/Colaboradores";
    private const string RutaTerminaciones = "/api/Colaboradores/Terminaciones";
    private const string RutaNombres = "/api/Colaboradores/Nombres";
    private const string SchemaColaboradores = "colaboradores";
    private const string TipoEventoNombresCorregidos = "nombres_corregidos";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // CA-3: la ausencia de evento es sincrona (sin proyeccion downstream) -- un timeout corto
    // alcanza para probarla sin alargar la suite esperando los 30s estandar sin ganar senal.
    private static readonly TimeSpan TimeoutAusencia = TimeSpan.FromSeconds(3);

    // Segunda lectura del mismo evento que ExisteEventoAsync ya espero: si el primer polling
    // termino, el evento esta -- no hay nada mas que esperar.
    private static readonly TimeSpan TimeoutLecturaConfirmada = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions OpcionesMarten =
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    // Numero unico por test -- evita colisiones entre ejecuciones repetidas del smoke test: la
    // identidad del stream es Identificacion.ToString() ("CC-<numero>"), no un Guid nuevo por
    // llamada, asi que reusar un numero fijo haria que el arrange (RegistrarColaborador) choque con
    // 409 en la segunda corrida. El formato "N" en MAYUSCULAS ya es alfanumerico ASCII, asi que
    // sobrevive intacto a la limpieza del numero (#381) y la llave esperada de abajo coincide con
    // la que arma el backend.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Issue #387: codigo URL-safe (unreserved RFC 3986) -- corregido de "[TEST]-" (corchetes
    // fuera del set permitido) a "TEST-" para que el arrange no falle con 400.
    private static string NuevoCodigoColaborador() => $"TEST-{Guid.CreateVersion7()}";

    // Oraculo independiente de la clave de stream (MEF-ADR-0002): se recompone aqui a mano, no se
    // deriva de Identificacion.ToString(), para que un cambio de formato en el VO no se auto-valide.
    // Separador "-" desde el issue #381.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    private static object PayloadRegistro(
        string numeroIdentificacion, DateOnly fechaInicio,
        string primerNombre, string? segundoNombre, string primerApellido, string? segundoApellido) => new
        {
            tipoIdentificacion = TipoIdentificacionCc,
            numeroIdentificacion,
            primerNombre,
            segundoNombre,
            primerApellido,
            segundoApellido,
            codigoColaborador = NuevoCodigoColaborador(),
            fechaInicio
        };

    private static object PayloadTerminacion(string numeroIdentificacion, DateOnly fechaEfectiva) => new
    {
        tipoIdentificacion = TipoIdentificacionCc,
        numeroIdentificacion,
        fechaEfectiva
    };

    private static object PayloadCorreccion(
        string numeroIdentificacion, string primerNombre, string? segundoNombre,
        string primerApellido, string? segundoApellido, string tipoIdentificacion = TipoIdentificacionCc) => new
        {
            tipoIdentificacion,
            numeroIdentificacion,
            primerNombre,
            segundoNombre,
            primerApellido,
            segundoApellido
        };

    // Arrange comun: registra un colaborador con una vinculacion abierta -- via el comando que la
    // origina (#330), nunca sembrando el event store por fuera del API.
    private async Task RegistrarColaboradorAsync(
        string numeroIdentificacion, DateOnly fechaInicio,
        string primerNombre, string? segundoNombre, string primerApellido, string? segundoApellido,
        CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            RutaRegistrar,
            PayloadRegistro(numeroIdentificacion, fechaInicio, primerNombre, segundoNombre, primerApellido, segundoApellido),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que RegistrarColaborador funcione");
    }

    // Arrange comun (CA-2): cierra la vinculacion vigente -- via el comando que la origina (#349),
    // nunca sembrando el event store por fuera del API.
    private async Task TerminarVinculacionAsync(
        string numeroIdentificacion, DateOnly fechaEfectiva, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            RutaTerminaciones, PayloadTerminacion(numeroIdentificacion, fechaEfectiva), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que TerminarVinculacion funcione");
    }

    // Assert comun de CA-1/CA-2: espera el evento en mt_events y verifica su payload por VALOR,
    // deserializando el campo Nombre con las opciones reales de Marten (el round-trip que ese VO
    // hace en produccion). No se compara contra el texto JSON persistido: mt_events.data es jsonb y
    // PostgreSQL no preserva ni whitespace ni orden de claves (docs 8.14.1), asi que cualquier
    // igualdad de texto seria un falso rojo -- la igualdad de dominio (NombreColaborador.Equals,
    // #348) es el oraculo correcto.
    private async Task ElStreamRecibioElNombreAsync(string streamId, NombreColaborador nombreEsperado)
    {
        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoNombresCorregidos, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoNombresCorregidos} deberia existir en el stream {streamId}");

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaColaboradores, streamId, TipoEventoNombresCorregidos, TimeoutLecturaConfirmada);

        var nombrePersistido = eventoPersistido.GetProperty("Nombre")
            .Deserialize<NombreColaborador>(OpcionesMarten);

        nombrePersistido.Should().Be(nombreEsperado);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-1: camino feliz -- colaborador con vinculacion abierta + nombre distinto por valor -> 202
    // y el stream recibe NombresCorregidos con el Nombre corregido. Sin Service Bus (event-sourcing
    // puro): mt_events es la unica ventana black-box a lo que quedo grabado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirNombres_Retorna202YPersisteNombresCorregidos_CuandoNombreEsDistintoPorValor()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(
            numeroIdentificacion, new DateOnly(2026, 1, 15),
            "[TEST]", "Original", "Smoke", null, ct);

        var response = await _client.PostAsJsonAsync(
            RutaNombres,
            PayloadCorreccion(numeroIdentificacion, "[TEST]", "Corregido", "Smoke", "Segundo"),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        await ElStreamRecibioElNombreAsync(
            ComputarStreamId(numeroIdentificacion),
            NombreColaborador.Crear("[TEST]", "Corregido", "Smoke", "Segundo"));
    }

    // CA-2: la ultima vinculacion esta TERMINADA -> la correccion procede igual -- solo exige
    // existencia del colaborador, nunca vigencia de su vinculacion (decision de refinamiento
    // 2026-08-11: los nombres son de la PERSONA, no de la vinculacion).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirNombres_Retorna202YPersisteNombresCorregidos_CuandoVinculacionEstaTerminada()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(
            numeroIdentificacion, new DateOnly(2026, 2, 1),
            "[TEST]", null, "Terminada", null, ct);
        await TerminarVinculacionAsync(numeroIdentificacion, new DateOnly(2026, 3, 1), ct);

        var response = await _client.PostAsJsonAsync(
            RutaNombres,
            PayloadCorreccion(numeroIdentificacion, "[TEST]", "Reingreso", "Terminada", null),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // La correccion procede sobre un colaborador con vinculacion terminada: solo exige
        // existencia, nunca vigencia.
        await ElStreamRecibioElNombreAsync(
            ComputarStreamId(numeroIdentificacion),
            NombreColaborador.Crear("[TEST]", "Reingreso", "Terminada", null));
    }

    // CA-3: nombre igual por valor al actual -> 202 sin evento nuevo en el stream (idempotencia
    // silenciosa). Verificacion de ausencia con timeout corto -- ver el porque en el comentario de
    // TimeoutAusencia (arriba).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirNombres_Retorna202SinNuevoEvento_CuandoNombreEsIgualPorValorAlActual()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(
            numeroIdentificacion, new DateOnly(2026, 1, 20),
            "[TEST]", "Igual", "Smoke", null, ct);

        var response = await _client.PostAsJsonAsync(
            RutaNombres,
            PayloadCorreccion(numeroIdentificacion, "[TEST]", "Igual", "Smoke", null),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, ComputarStreamId(numeroIdentificacion), TipoEventoNombresCorregidos,
            TimeoutAusencia);

        existe.Should().BeFalse(
            "un nombre igual por valor al actual no deberia persistir un evento nuevo (idempotencia silenciosa)");
    }

    // CA-4: colaborador inexistente -> 404, sin escribir nada al event store (no hay stream para
    // consultar: la ausencia de escritura la garantiza el propio 404 -- el handler lanza antes de
    // llegar al aggregate).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirNombres_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion(); // nunca registrado

        var response = await _client.PostAsJsonAsync(
            RutaNombres,
            PayloadCorreccion(numeroIdentificacion, "[TEST]", null, "Smoke", null),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // CA-5: PrimerNombre vacio -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirNombres_Retorna400_CuandoPrimerNombreEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadCorreccion(
            NuevoNumeroIdentificacion(), primerNombre: "", segundoNombre: null,
            primerApellido: "Smoke", segundoApellido: null);

        var response = await _client.PostAsJsonAsync(RutaNombres, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-5: PrimerApellido vacio -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirNombres_Retorna400_CuandoPrimerApellidoEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadCorreccion(
            NuevoNumeroIdentificacion(), primerNombre: "[TEST]", segundoNombre: null,
            primerApellido: "", segundoApellido: null);

        var response = await _client.PostAsJsonAsync(RutaNombres, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-5: NumeroIdentificacion vacio -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirNombres_Retorna400_CuandoNumeroIdentificacionEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadCorreccion(
            numeroIdentificacion: "", primerNombre: "[TEST]", segundoNombre: null,
            primerApellido: "Smoke", segundoApellido: null);

        var response = await _client.PostAsJsonAsync(RutaNombres, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-5: TipoIdentificacion fuera de la lista cerrada (PILA: CC, CE, TI, PA, PT) -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirNombres_Retorna400_CuandoTipoIdentificacionNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadCorreccion(
            NuevoNumeroIdentificacion(), primerNombre: "[TEST]", segundoNombre: null,
            primerApellido: "Smoke", segundoApellido: null, tipoIdentificacion: "XX");

        var response = await _client.PostAsJsonAsync(RutaNombres, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
