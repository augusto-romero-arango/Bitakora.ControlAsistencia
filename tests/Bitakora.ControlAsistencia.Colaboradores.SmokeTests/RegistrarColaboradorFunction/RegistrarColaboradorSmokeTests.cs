using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.RegistrarColaboradorFunction;

public class RegistrarColaboradorSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    // Sin test de "ruta PascalCase -> 404": el routing de Azure Functions es case-insensitive y
    // /api/Colaboradores sigue resolviendo a este endpoint.
    private const string RutaRegistrar = "/api/colaboradores";
    private const string SchemaColaboradores = "colaboradores";
    private const string TipoEventoColaboradorRegistrado = "colaborador_registrado";
    private const string TipoEventoVinculacionIniciada = "vinculacion_iniciada";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Unico por test: el stream se llama por la identificacion, asi que repetir un numero choca con 409
    // en la segunda corrida. "N" en mayusculas sobrevive a la limpieza del numero y la llave coincide.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Siempre canonico: TipoIdentificacion.Desde normaliza, asi que el stream esperado no depende del
    // casing del payload. Oraculo independiente (MEF-ADR-0002): se recompone a mano.
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

    // Separado de PayloadRegistro a proposito: sin sede, el campo codigoSede debe ir AUSENTE, no
    // presente en null.
    private static object PayloadRegistroConSede(
        string numeroIdentificacion, DateOnly fechaInicio, string? codigoSede) => new
        {
            tipoIdentificacion = TipoIdentificacionCc,
            numeroIdentificacion,
            primerNombre = "[TEST]",
            segundoNombre = (string?)null,
            primerApellido = "Smoke",
            segundoApellido = (string?)null,
            codigoColaborador = NuevoCodigoColaborador(),
            fechaInicio,
            codigoSede
        };

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
    public async Task RegistrarColaborador_Retorna201YPersisteColaboradorRegistradoYVinculacionIniciada_CuandoIdentificacionNoExiste()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 1, 15);

        var response = await _client.PostAsJsonAsync(
            RutaRegistrar, PayloadRegistro(numeroIdentificacion, fechaInicio), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var streamId = ComputarStreamId(numeroIdentificacion);
        response.Headers.Location.Should().Be(
            new Uri($"/api/colaboradores/fichas/{streamId}", UriKind.Relative));

        var existeRegistrado = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoColaboradorRegistrado, Timeout);

        existeRegistrado.Should().BeTrue(
            $"el evento {TipoEventoColaboradorRegistrado} deberia existir en el stream {streamId}");

        var existeVinculacion = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionIniciada, Timeout);

        existeVinculacion.Should().BeTrue(
            $"el evento {TipoEventoVinculacionIniciada} deberia existir en el mismo commit que {TipoEventoColaboradorRegistrado}");
    }

    // El borde ya no normaliza: si TipoIdentificacion.Desde dejara de hacerlo, el evento no apareceria en
    // el stream canonico.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna201YPersisteEnElStreamCanonico_CuandoTipoIdentificacionLlegaEnMinusculasConEspacios()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 2, 1);

        var response = await _client.PostAsJsonAsync(
            RutaRegistrar,
            PayloadRegistro(numeroIdentificacion, fechaInicio, tipoIdentificacion: " cc "),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var streamIdCanonico = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamIdCanonico, TipoEventoColaboradorRegistrado, Timeout);

        existe.Should().BeTrue(
            $"' cc ' deberia normalizar a la instancia canonica CC y persistir en el stream {streamIdCanonico}, nunca en uno separado por casing");
    }

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
        primerRegistro.StatusCode.Should().Be(HttpStatusCode.Created,
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

    // "CC" y " cc " colapsan a la misma clave porque Desde normaliza antes del lookup.
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
        primerRegistro.StatusCode.Should().Be(HttpStatusCode.Created,
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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna400_CuandoNumeroIdentificacionEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadRegistro("", new DateOnly(2026, 1, 1));

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

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

    // default(DateOnly) es "no llego" segun la doctrina bitemporal del BC.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna400_CuandoFechaInicioEsVacia()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadRegistro(NuevoNumeroIdentificacion(), default(DateOnly));

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Desde normaliza y LUEGO rechaza contra la lista cerrada, sin importar el casing.
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

    // NuevoCodigoColaborador solo ejercita alfanumerico + guion: esto confirma que el regex desplegado no
    // es mas restrictivo que el unreserved de RFC 3986 seccion 2.3.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna201_CuandoCodigoColaboradorTieneCaracteresUnreservedNoAlfanumericos()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var codigoColaborador = $"a.b_{Guid.CreateVersion7()}~2";

        var response = await _client.PostAsJsonAsync(
            RutaRegistrar,
            PayloadRegistro(numeroIdentificacion, new DateOnly(2026, 4, 1), codigoColaborador: codigoColaborador),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoColaboradorRegistrado, Timeout);

        existe.Should().BeTrue(
            $"el codigo con caracteres unreserved no alfanumericos deberia haberse aceptado y persistido en {streamId}");
    }

    // ":" es el separador de accion de la ruta (vinculaciones/{codigo}:terminar, MEF-ADR-0043 seccion 1).
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

    // La exhaustividad del regex la cubre RegistrarColaboradorValidatorTests; aqui solo que la regla
    // llega desplegada.
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

    // La sede viaja DENTRO de VinculacionIniciada, nunca como un SedeAsignada adicional en el commit.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna201YPersisteVinculacionIniciadaConCodigoSede_CuandoBodyTraeCodigoSede()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 4, 9);

        var response = await _client.PostAsJsonAsync(
            RutaRegistrar,
            PayloadRegistroConSede(numeroIdentificacion, fechaInicio, codigoSede: "BOG"),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaColaboradores, streamId, TipoEventoVinculacionIniciada, Timeout);

        eventoPersistido.GetProperty("CodigoSede").GetString().Should().Be("BOG");
    }

    // Evolucion aditiva: un body sin el campo sigue siendo valido.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna201YPersisteVinculacionIniciadaConCodigoSedeNulo_CuandoBodyNoTraeCodigoSede()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 4, 10);

        var response = await _client.PostAsJsonAsync(
            RutaRegistrar, PayloadRegistro(numeroIdentificacion, fechaInicio), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaColaboradores, streamId, TipoEventoVinculacionIniciada, Timeout);

        eventoPersistido.GetProperty("CodigoSede").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // Opcional = ausente es valido; presente exige valor.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna400_CuandoCodigoSedeEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadRegistroConSede(
            NuevoNumeroIdentificacion(), new DateOnly(2026, 4, 11), codigoSede: "");

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // FluentValidation NotEmpty tambien rechaza solo espacios.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_Retorna400_CuandoCodigoSedeEsBlanco()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadRegistroConSede(
            NuevoNumeroIdentificacion(), new DateOnly(2026, 4, 11), codigoSede: "   ");

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
