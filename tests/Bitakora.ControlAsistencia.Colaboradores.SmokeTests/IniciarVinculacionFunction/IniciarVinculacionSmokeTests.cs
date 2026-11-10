using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.IniciarVinculacionFunction;

public class IniciarVinculacionSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/colaboradores";
    private const string RutaReingresosVieja = "/api/Colaboradores/Reingresos";
    private const string SchemaColaboradores = "colaboradores";
    private const string TipoEventoVinculacionIniciada = "vinculacion_iniciada";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Unico por test: el stream se llama por la identificacion, asi que repetir un numero choca con 409
    // en la segunda corrida. "N" en mayusculas sobrevive a la limpieza del numero y la llave coincide.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Oraculo independiente (MEF-ADR-0002): se recompone a mano, no se deriva de Identificacion.ToString(),
    // para que un cambio de formato en el VO no se auto-valide.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    // Separado de ComputarStreamId a proposito: que la entrada de la request y el oraculo coincidan es
    // lo que se prueba.
    private static string IdDeRuta(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    private static string FormatearFecha(DateOnly fecha) =>
        fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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

    // Separado de PayloadIniciarVinculacion a proposito: sin sede, el campo codigoSede debe ir AUSENTE,
    // no presente en null.
    private static object PayloadIniciarVinculacionConSede(
        string codigoColaborador, DateOnly fechaInicio, string? codigoSede) => new
        {
            codigoColaborador,
            fechaInicio,
            codigoSede
        };

    // Devuelve el codigo de la vinculacion inicial (== CodigoColaborador, ver
    // ColaboradorAggregateRoot.Registrar), que el arrange usa como {codigo} al terminar.
    private async Task<string> RegistrarColaboradorAsync(
        string numeroIdentificacion, DateOnly fechaInicio, CancellationToken ct)
    {
        var codigo = NuevoCodigoColaborador();

        var response = await _client.PostAsJsonAsync(
            RutaRegistrar, PayloadRegistro(numeroIdentificacion, fechaInicio, codigo), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que RegistrarColaborador funcione");

        return codigo;
    }

    private async Task TerminarVinculacionAsync(
        string id, string codigo, DateOnly fechaEfectiva, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/colaboradores/{id}/vinculaciones/{codigo}:terminar",
            new { fechaEfectiva },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que TerminarVinculacion funcione");
    }

    private Task<HttpResponseMessage> IniciarVinculacionAsync(
        string id, string codigoColaborador, DateOnly fechaInicio, CancellationToken ct) =>
        _client.PostAsJsonAsync(
            $"/api/colaboradores/{id}/vinculaciones",
            PayloadIniciarVinculacion(codigoColaborador, fechaInicio),
            ct);

    private Task<HttpResponseMessage> IniciarVinculacionConSedeAsync(
        string id, string codigoColaborador, DateOnly fechaInicio, string? codigoSede, CancellationToken ct) =>
        _client.PostAsJsonAsync(
            $"/api/colaboradores/{id}/vinculaciones",
            PayloadIniciarVinculacionConSede(codigoColaborador, fechaInicio, codigoSede),
            ct);

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
    public async Task IniciarVinculacion_Retorna201YPersisteVinculacionIniciada_CuandoFechaInicioEsPosteriorATerminacion()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicioOriginal = new DateOnly(2025, 1, 15);
        var fechaEfectivaTerminacion = new DateOnly(2025, 6, 30);
        var fechaNuevaVinculacion = new DateOnly(2025, 7, 1);
        var codigoNuevo = NuevoCodigoColaborador();

        var codigoVigente = await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicioOriginal, ct);
        await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoVigente, fechaEfectivaTerminacion, ct);

        var response = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoNuevo, fechaNuevaVinculacion, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().Be(
            new Uri($"/api/colaboradores/fichas/{IdDeRuta(numeroIdentificacion)}", UriKind.Relative));

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionIniciada, Timeout,
            campoJson: "Codigo", valorJson: codigoNuevo);

        existe.Should().BeTrue(
            $"el evento {TipoEventoVinculacionIniciada} con Codigo {codigoNuevo} deberia existir en el stream {streamId}");

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaColaboradores, streamId, TipoEventoVinculacionIniciada,
            campoJson: "Codigo", valorJson: codigoNuevo, Timeout);

        eventoPersistido.GetProperty("FechaInicio").GetString().Should().Be(FormatearFecha(fechaNuevaVinculacion));
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna201YPersisteVinculacionIniciada_CuandoTerminacionFuePreavisoFuturo()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicioOriginal = new DateOnly(2025, 1, 1);
        // Muy en el futuro: ninguna fecha se valida contra el reloj del servidor.
        var fechaEfectivaPreaviso = new DateOnly(2030, 12, 31);
        var fechaNuevaVinculacion = new DateOnly(2031, 1, 1);
        var codigoNuevo = NuevoCodigoColaborador();

        var codigoVigente = await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicioOriginal, ct);
        await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoVigente, fechaEfectivaPreaviso, ct);

        var response = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoNuevo, fechaNuevaVinculacion, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionIniciada, Timeout,
            campoJson: "Codigo", valorJson: codigoNuevo);

        existe.Should().BeTrue(
            "la nueva vinculacion posterior a un preaviso futuro deberia aceptarse sin validar contra el reloj del servidor");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna409_CuandoVinculacionNuncaFueTerminada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2025, 3, 1), ct);

        var response = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), new DateOnly(2025, 4, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // Regresion de Apply(VinculacionIniciada): si no reabriera la vinculacion, el segundo intento
    // heredaria en falso la terminacion original y se aceptaria.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna409_CuandoYaFueIniciadaSinTerminarDeNuevo()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaEfectivaTerminacion = new DateOnly(2025, 3, 1);
        var fechaPrimeraVinculacionNueva = new DateOnly(2025, 3, 15);

        var codigoVigente = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2025, 1, 1), ct);
        await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoVigente, fechaEfectivaTerminacion, ct);

        var primeraVinculacionNueva = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), fechaPrimeraVinculacionNueva, ct);

        primeraVinculacionNueva.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que la primera vinculacion nueva funcione");

        var segundaVinculacionNueva = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), new DateOnly(2025, 4, 1), ct);

        segundaVinculacionNueva.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // El dia de la FechaEfectiva pertenece a la vinculacion que termina.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna409_CuandoFechaInicioEsIgualAFechaEfectivaDeTerminacion()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaEfectivaTerminacion = new DateOnly(2025, 6, 1);

        var codigoVigente = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2025, 1, 1), ct);
        await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoVigente, fechaEfectivaTerminacion, ct);

        var response = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), fechaEfectivaTerminacion, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna409_CuandoFechaInicioEsAnteriorAFechaEfectivaDeTerminacion()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaEfectivaTerminacion = new DateOnly(2025, 6, 1);
        var fechaAnterior = new DateOnly(2025, 5, 1);

        var codigoVigente = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2025, 1, 1), ct);
        await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoVigente, fechaEfectivaTerminacion, ct);

        var response = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), fechaAnterior, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // El preaviso deja la vinculacion "no abierta", pero la FechaInicio sigue teniendo que ser
    // estrictamente posterior a la FechaEfectiva.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna409_CuandoFechaInicioNoSuperaElPreavisoNoVencido()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaEfectivaPreaviso = new DateOnly(2030, 12, 31);
        var fechaAnteriorAlPreaviso = new DateOnly(2026, 1, 1);

        var codigoVigente = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2025, 1, 1), ct);
        await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoVigente, fechaEfectivaPreaviso, ct);

        var response = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), fechaAnteriorAlPreaviso, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        var response = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), new DateOnly(2025, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await IniciarVinculacionAsync(
            $"{TipoIdentificacionCc}{NuevoNumeroIdentificacion()}",
            NuevoCodigoColaborador(), new DateOnly(2025, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna400_CuandoTipoDeLaIdentificacionDeRutaNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await IniciarVinculacionAsync(
            $"XX-{NuevoNumeroIdentificacion()}", NuevoCodigoColaborador(), new DateOnly(2025, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna400_CuandoElNumeroDelIdDeRutaQuedaVacio()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await IniciarVinculacionAsync(
            $"{TipoIdentificacionCc}-", NuevoCodigoColaborador(), new DateOnly(2025, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna400_CuandoCodigoColaboradorEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await IniciarVinculacionAsync(
            IdDeRuta(NuevoNumeroIdentificacion()), codigoColaborador: "", new DateOnly(2025, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // default(DateOnly) es "no llego" segun la doctrina bitemporal del BC.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna400_CuandoFechaInicioEsVacia()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await IniciarVinculacionAsync(
            IdDeRuta(NuevoNumeroIdentificacion()), NuevoCodigoColaborador(), default(DateOnly), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Afirmativo contra el entorno: la ausencia de referencias en la suite no distingue "la ruta se
    // elimino" de "sigue viva y nadie la llama".
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna404DelHost_CuandoSeLlamaLaRutaViejaPost()
    {
        var ct = TestContext.Current.CancellationToken;

        // codigoColaborador vacio a proposito: con un body valido la ruta vieja, si siguiera viva,
        // responderia un 404 de dominio indistinguible del 404 del host; con este responde 400 de su
        // validador (comprobado contra dev con la revision anterior).
        var response = await _client.PostAsJsonAsync(
            RutaReingresosVieja,
            new
            {
                tipoIdentificacion = TipoIdentificacionCc,
                numeroIdentificacion = NuevoNumeroIdentificacion(),
                codigoColaborador = "",
                fechaInicio = new DateOnly(2025, 3, 1)
            },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "POST Colaboradores/Reingresos se reemplazo por POST colaboradores/{id}/vinculaciones (issue #378): un 400 aqui delataria que la ruta vieja sigue viva");
    }

    // NuevoCodigoColaborador solo ejercita alfanumerico + guion: esto confirma que el regex desplegado no
    // es mas restrictivo que el unreserved de RFC 3986 seccion 2.3.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna201_CuandoCodigoColaboradorTieneCaracteresUnreservedNoAlfanumericos()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaEfectivaTerminacion = new DateOnly(2025, 6, 30);
        var fechaNuevaVinculacion = new DateOnly(2025, 7, 1);
        var codigoNuevo = $"a.b_{Guid.CreateVersion7()}~2";

        var codigoVigente = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2025, 1, 15), ct);
        await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoVigente, fechaEfectivaTerminacion, ct);

        var response = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoNuevo, fechaNuevaVinculacion, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionIniciada, Timeout,
            campoJson: "Codigo", valorJson: codigoNuevo);

        existe.Should().BeTrue(
            $"el codigo con caracteres unreserved no alfanumericos deberia haberse aceptado y persistido en {streamId}");
    }

    // ":" es el separador de accion de la ruta (vinculaciones/{codigo}:terminar, MEF-ADR-0043 seccion 1).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna400_CuandoCodigoColaboradorContieneDosPuntos()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = $"COL:{Guid.CreateVersion7()}";

        var response = await IniciarVinculacionAsync(
            IdDeRuta(NuevoNumeroIdentificacion()), codigoColaborador, new DateOnly(2025, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // La exhaustividad del regex la cubre IniciarVinculacionBodyValidatorTests; aqui solo que la regla
    // llega desplegada.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna400_CuandoCodigoColaboradorContieneEspacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = $"COL {Guid.CreateVersion7()}";

        var response = await IniciarVinculacionAsync(
            IdDeRuta(NuevoNumeroIdentificacion()), codigoColaborador, new DateOnly(2025, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // No se verifica la sede anterior: el dominio no expone una vista de la sede vigente.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna201YPersisteVinculacionIniciadaConCodigoSede_CuandoBodyTraeCodigoSede()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaEfectivaTerminacion = new DateOnly(2026, 1, 30);
        var fechaNuevaVinculacion = new DateOnly(2026, 2, 1);
        var codigoNuevo = NuevoCodigoColaborador();

        var codigoVigente = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 1), ct);
        await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoVigente, fechaEfectivaTerminacion, ct);

        var response = await IniciarVinculacionConSedeAsync(
            IdDeRuta(numeroIdentificacion), codigoNuevo, fechaNuevaVinculacion, codigoSede: "MED", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaColaboradores, streamId, TipoEventoVinculacionIniciada,
            campoJson: "Codigo", valorJson: codigoNuevo, Timeout);

        eventoPersistido.GetProperty("CodigoSede").GetString().Should().Be("MED");
    }

    // Sin sede, el reingreso nace limpio.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna201YPersisteVinculacionIniciadaConCodigoSedeNulo_CuandoBodyNoTraeCodigoSede()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaEfectivaTerminacion = new DateOnly(2026, 2, 27);
        var fechaNuevaVinculacion = new DateOnly(2026, 3, 1);
        var codigoNuevo = NuevoCodigoColaborador();

        var codigoVigente = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 1), ct);
        await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoVigente, fechaEfectivaTerminacion, ct);

        var response = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoNuevo, fechaNuevaVinculacion, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaColaboradores, streamId, TipoEventoVinculacionIniciada,
            campoJson: "Codigo", valorJson: codigoNuevo, Timeout);

        eventoPersistido.GetProperty("CodigoSede").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna400_CuandoCodigoSedeEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await IniciarVinculacionConSedeAsync(
            IdDeRuta(NuevoNumeroIdentificacion()), NuevoCodigoColaborador(), new DateOnly(2025, 3, 1),
            codigoSede: "", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // FluentValidation NotEmpty tambien rechaza solo espacios.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna400_CuandoCodigoSedeEsBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await IniciarVinculacionConSedeAsync(
            IdDeRuta(NuevoNumeroIdentificacion()), NuevoCodigoColaborador(), new DateOnly(2025, 3, 1),
            codigoSede: "   ", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // La sede opcional no agrega ni quita razones de rechazo.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna409_CuandoVinculacionNuncaFueTerminadaYBodyTraeCodigoSede()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2025, 3, 1), ct);

        var response = await IniciarVinculacionConSedeAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), new DateOnly(2025, 4, 1),
            codigoSede: "BOG", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
