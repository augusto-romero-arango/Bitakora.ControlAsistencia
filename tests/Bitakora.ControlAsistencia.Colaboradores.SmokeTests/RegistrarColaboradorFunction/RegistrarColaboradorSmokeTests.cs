// Issue #330: smoke tests del endpoint POST Colaboradores (registrar un colaborador bajo control
// de asistencia -- primer comando del ciclo de vida de ColaboradorAggregateRoot, desglose
// #348-#357). Molde: TerminarVinculacionSmokeTests/IniciarVinculacionSmokeTests -- mismo comando
// event-sourcing puro sin consumidores downstream (CA-ADR-0030): sin ServiceBusFixture, la unica
// verificacion black-box de los efectos del handler es leer mt_events via PostgresFixture.
//
// Este archivo no existia antes del issue #371 (RegistrarColaboradorFunction no tenia smoke tests
// dedicados; solo se usaba como arrange comun de los demas comandos del dominio). Se crea ahora
// porque el issue #371 modifico su CommandHandler y Validator (movio la normalizacion de
// TipoIdentificacion del borde al VO) y la cobertura black-box de ese endpoint quedaba en blanco.
//
// Issue #371 (foco de este archivo): TipoIdentificacion.Desde ya normaliza (trim + MAYUSCULAS
// invariante) internamente -- el borde HTTP dejo de repetir esa normalizacion. El comportamiento
// EXTERNO no cambia (ya normalizaba antes en el borde), pero el punto donde vive el conocimiento
// si cambio, y eso es exactamente lo que las CA-1/CA-1-bis de abajo verifican end-to-end contra el
// entorno real: un TipoIdentificacion con espacios y minusculas debe seguir colapsando a la MISMA
// clave de stream canonica ("CC-<numero>"), nunca abrir una segunda clave ("cc-<numero>").
//
// CA-1 (ruta de exito): identificacion nueva -> 202 y el stream recibe, en un solo commit,
// ColaboradorRegistrado + VinculacionIniciada (issue #330).
// CA-1-bis (#371): mismo camino feliz, pero con TipoIdentificacion sin normalizar en el payload ->
// 202 y el evento aparece en el stream CANONICO, no en uno alterno por casing.
// CA-2 (duplicado exacto): misma Identificacion ya registrada -> 409, sin escribir un segundo
// colaborador_registrado.
// CA-2-bis (#371, corazon del refactor): el duplicado tambien se detecta cuando el segundo request
// llega con TipoIdentificacion sin normalizar -- "CC" y " cc " deben colapsar a la MISMA clave de
// stream, asi que el aggregate ya existe y el segundo registro se rechaza igual que con el mismo
// casing exacto.
// CA-3: request invalida (NumeroIdentificacion/CodigoColaborador vacios, FechaInicio vacia, tipo
// fuera de la lista cerrada) -> 400, sin tocar el event store.
//
// Issue #387 (CodigoColaborador URL-safe): CA-1 con caracteres unreserved no alfanumericos (. _ ~)
// -> 202 (el set permitido no se limita a alfanumerico+guion); CA-2/CA-3 con ":" (separador de
// accion reservado, MEF-ADR-0043) y espacio (fuera del set unreserved RFC 3986) -> 400.
//
// Issue #378 (CA-5): la ruta paso de "Colaboradores" (PascalCase) a "colaboradores" (kebab-case
// minusculo, MEF-ADR-0043 seccion 3) -- sin cambio de verbo ni forma. RutaRegistrar se actualiza a
// la ruta nueva para reflejar el contrato vigente; no se agrega un assert de "la ruta vieja
// PascalCase da 404 del host" (a diferencia de CorregirNombresSmokeTests CA-5, que si cambio de
// verbo Y de forma): el routing HTTP de Azure Functions/ASP.NET Core hace matching de rutas
// case-insensitive por defecto, asi que un POST a /api/Colaboradores seguiria resolviendo al mismo
// endpoint -- afirmar 404 alli seria un test black-box incorrecto contra el comportamiento real del
// host, no una verificacion de este cambio.
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.RegistrarColaboradorFunction;

public class RegistrarColaboradorSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/colaboradores";
    private const string SchemaColaboradores = "colaboradores";
    private const string TipoEventoColaboradorRegistrado = "colaborador_registrado";
    private const string TipoEventoVinculacionIniciada = "vinculacion_iniciada";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Numero unico por test -- evita colisiones entre ejecuciones repetidas del smoke test: la
    // identidad del stream es Identificacion.ToString() ("CC-<numero>"), no un Guid nuevo por
    // llamada, asi que reusar un numero fijo haria que el segundo registro choque con 409 en la
    // segunda corrida. El formato "N" en MAYUSCULAS ya es alfanumerico ASCII, asi que sobrevive
    // intacto a la limpieza del numero (#381) y la llave esperada de abajo coincide con la que
    // arma el backend.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Siempre canonico ("CC-<numero>", separador "-" desde el issue #381): TipoIdentificacion.Desde
    // nunca almacena el input crudo, solo retorna la instancia canonica de la lista cerrada (issue
    // #371) -- por eso el streamId esperado no varia sin importar el casing con el que llego el
    // TipoIdentificacion en el payload. Oraculo independiente (MEF-ADR-0002): se recompone a mano,
    // no se deriva de Identificacion.ToString().
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    private static object PayloadRegistro(
        string numeroIdentificacion, DateOnly fechaInicio,
        string tipoIdentificacion = TipoIdentificacionCc, string? codigoColaborador = null) => new
        {
            tipoIdentificacion,
            numeroIdentificacion,
            primerNombre = "[TEST]",
            segundoNombre = (string?)null,
            primerApellido = "Smoke",
            segundoApellido = (string?)null,
            codigoColaborador = codigoColaborador ?? NuevoCodigoColaborador(),
            fechaInicio
        };

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-1: camino feliz -- identificacion nueva -> 202 y el stream recibe, en un solo commit,
    // ColaboradorRegistrado + VinculacionIniciada. Sin Service Bus (event-sourcing puro): mt_events
    // es la unica ventana black-box a lo que quedo grabado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna202YPersisteColaboradorRegistradoYVinculacionIniciada_CuandoIdentificacionNoExiste()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 1, 15);

        var response = await _client.PostAsJsonAsync(
            RutaRegistrar, PayloadRegistro(numeroIdentificacion, fechaInicio), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existeRegistrado = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoColaboradorRegistrado, Timeout);

        existeRegistrado.Should().BeTrue(
            $"el evento {TipoEventoColaboradorRegistrado} deberia existir en el stream {streamId}");

        var existeVinculacion = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionIniciada, Timeout);

        existeVinculacion.Should().BeTrue(
            $"el evento {TipoEventoVinculacionIniciada} deberia existir en el mismo commit que {TipoEventoColaboradorRegistrado}");
    }

    // CA-1-bis (issue #371): la normalizacion vive ahora en TipoIdentificacion.Desde -- un
    // TipoIdentificacion con espacios y minusculas debe colapsar a la misma clave de stream
    // canonica ("CC-<numero>"), nunca abrir una segunda clave ("cc-<numero>"). Verificacion
    // end-to-end del refactor: el borde ya no normaliza, asi que si el VO dejara de hacerlo este
    // test dejaria de encontrar el evento en el stream canonico.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna202YPersisteEnElStreamCanonico_CuandoTipoIdentificacionLlegaEnMinusculasConEspacios()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 2, 1);

        var response = await _client.PostAsJsonAsync(
            RutaRegistrar,
            PayloadRegistro(numeroIdentificacion, fechaInicio, tipoIdentificacion: " cc "),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamIdCanonico = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamIdCanonico, TipoEventoColaboradorRegistrado, Timeout);

        existe.Should().BeTrue(
            $"' cc ' deberia normalizar a la instancia canonica CC y persistir en el stream {streamIdCanonico}, nunca en uno separado por casing");
    }

    // CA-2: duplicado exacto -- misma Identificacion ya registrada -> 409, sin escribir un segundo
    // colaborador_registrado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna409_CuandoIdentificacionYaExiste()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 3, 1);

        var primerRegistro = await _client.PostAsJsonAsync(
            RutaRegistrar, PayloadRegistro(numeroIdentificacion, fechaInicio), ct);
        primerRegistro.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que el primer registro funcione");

        var streamId = ComputarStreamId(numeroIdentificacion);
        var existePrimerRegistro = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoColaboradorRegistrado, Timeout);
        existePrimerRegistro.Should().BeTrue(
            $"el evento {TipoEventoColaboradorRegistrado} del primer registro deberia estar en el stream {streamId} antes de reintentar");

        var segundoRegistro = await _client.PostAsJsonAsync(
            RutaRegistrar, PayloadRegistro(numeroIdentificacion, fechaInicio), ct);

        segundoRegistro.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var registros = await postgres.ContarEventosAsync(
            SchemaColaboradores, streamId, TipoEventoColaboradorRegistrado);

        registros.Should().Be(1,
            "el segundo registro se rechazo con 409: no debe haber escrito un segundo colaborador_registrado");
    }

    // CA-2-bis (issue #371, corazon del refactor): el duplicado tambien se detecta cuando el
    // segundo request llega con TipoIdentificacion sin normalizar -- "CC" y " cc " colapsan a la
    // MISMA clave de stream ("CC-<numero>") porque Desde normaliza antes del lookup, asi que el
    // aggregate ya existe y el segundo registro se rechaza igual que con el mismo casing exacto.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna409YNoDuplicaEvento_CuandoSegundoRegistroLlegaConTipoIdentificacionSinNormalizar()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 3, 10);

        var primerRegistro = await _client.PostAsJsonAsync(
            RutaRegistrar,
            PayloadRegistro(numeroIdentificacion, fechaInicio, tipoIdentificacion: TipoIdentificacionCc),
            ct);
        primerRegistro.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que el primer registro (con 'CC') funcione");

        var streamId = ComputarStreamId(numeroIdentificacion);
        var existePrimerRegistro = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoColaboradorRegistrado, Timeout);
        existePrimerRegistro.Should().BeTrue(
            $"el evento {TipoEventoColaboradorRegistrado} del primer registro (con 'CC') deberia estar en el stream {streamId} antes de reintentar con ' cc '");

        var segundoRegistro = await _client.PostAsJsonAsync(
            RutaRegistrar,
            PayloadRegistro(numeroIdentificacion, fechaInicio, tipoIdentificacion: " cc "),
            ct);

        segundoRegistro.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "'CC' y ' cc ' deberian colapsar a la misma Identificacion (TipoIdentificacion.Desde normaliza) y colisionar en el mismo stream");

        var registros = await postgres.ContarEventosAsync(
            SchemaColaboradores, streamId, TipoEventoColaboradorRegistrado);

        registros.Should().Be(1,
            "el segundo registro (con casing distinto) se rechazo con 409: no debe haber abierto un stream separado ni duplicado el evento");
    }

    // CA-3: NumeroIdentificacion vacio -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna400_CuandoNumeroIdentificacionEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadRegistro("", new DateOnly(2026, 1, 1));

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-3: CodigoColaborador vacio -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna400_CuandoCodigoColaboradorEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadRegistro(
            NuevoNumeroIdentificacion(), new DateOnly(2026, 1, 1), codigoColaborador: "");

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-3: FechaInicio vacia (default de DateOnly, "no llego" segun la doctrina bitemporal del BC)
    // -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna400_CuandoFechaInicioEsVacia()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadRegistro(NuevoNumeroIdentificacion(), default(DateOnly));

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-3: TipoIdentificacion fuera de la lista cerrada (PILA: CC, CE, TI, PA, PT) -> 400, sin
    // importar el casing -- Desde normaliza y LUEGO rechaza contra el diccionario cerrado (#371).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna400_CuandoTipoIdentificacionNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadRegistro(
            NuevoNumeroIdentificacion(), new DateOnly(2026, 1, 1), tipoIdentificacion: "XX");

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-1 (#387): codigo con caracteres unreserved no alfanumericos (. _ ~) tambien produce 202 --
    // el set permitido no se limita a alfanumerico+guion, que es lo unico que ejercita el helper
    // compartido NuevoCodigoColaborador ("TEST-<guid>"). Verificacion end-to-end de que el regex
    // desplegado en dev no es mas restrictivo que el unreserved de RFC 3986 seccion 2.3.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna202_CuandoCodigoColaboradorTieneCaracteresUnreservedNoAlfanumericos()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var codigoColaborador = $"a.b_{Guid.CreateVersion7()}~2";

        var response = await _client.PostAsJsonAsync(
            RutaRegistrar,
            PayloadRegistro(numeroIdentificacion, new DateOnly(2026, 4, 1), codigoColaborador: codigoColaborador),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoColaboradorRegistrado, Timeout);

        existe.Should().BeTrue(
            $"el codigo con caracteres unreserved no alfanumericos deberia haberse aceptado y persistido en {streamId}");
    }

    // CA-2 (#387): ":" esta explicitamente fuera del set permitido -- MEF-ADR-0043 seccion 1 lo
    // reserva como separador de accion (vinculaciones/{codigo}:terminar, #379). Un codigo con ":"
    // haria inparseable esa ruta -- caso destacado del issue.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna400_CuandoCodigoColaboradorContieneDosPuntos()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = $"COL:{Guid.CreateVersion7()}";
        var payload = PayloadRegistro(
            NuevoNumeroIdentificacion(), new DateOnly(2026, 4, 1), codigoColaborador: codigoColaborador);

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-3 (#387): cualquier otro caracter fuera del set (espacio, aqui) -> 400. La exhaustividad
    // del regex (acento, "/") ya la cubre RegistrarColaboradorValidatorTests; este smoke test solo
    // confirma que la regla llega desplegada end-to-end en dev.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna400_CuandoCodigoColaboradorContieneEspacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = $"COL {Guid.CreateVersion7()}";
        var payload = PayloadRegistro(
            NuevoNumeroIdentificacion(), new DateOnly(2026, 4, 1), codigoColaborador: codigoColaborador);

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
