using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.RetirarEtiquetaFunction;

public class RetirarEtiquetaSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/colaboradores";
    private const string SchemaColaboradores = "colaboradores";
    private const string TipoEventoEtiquetaAsignada = "etiqueta_asignada";
    // CategoriaNormalizada es un campo escalar: a diferencia de etiqueta_asignada, etiqueta_retirada se
    // puede filtrar por (campoJson, valorJson) aunque el stream acumule varios.
    private const string TipoEventoEtiquetaRetirada = "etiqueta_retirada";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // La escritura es sincrona (sin proyeccion downstream): un timeout corto basta para probar la ausencia.
    private static readonly TimeSpan TimeoutAusencia = TimeSpan.FromSeconds(3);

    // Unico por test: el stream se llama por la identificacion, asi que repetir un numero choca con 409
    // en la segunda corrida. "N" en mayusculas sobrevive a la limpieza del numero y la llave coincide.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Oraculo independiente (MEF-ADR-0002): se recompone a mano, no se deriva de Identificacion.ToString(),
    // para que un cambio de formato en el VO no se auto-valide. Coincide con el {id} de ruta y se reusa.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    // AsignarEtiqueta (PUT) y RetirarEtiqueta (DELETE) comparten la ruta; se distinguen por el verbo.
    private static string RutaEtiqueta(string id, string categoria) =>
        $"/api/colaboradores/{id}/etiquetas/{Uri.EscapeDataString(categoria)}";

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

    private static object PayloadValor(string valor) => new { valor };

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

    private async Task AsignarEtiquetaAsync(
        string id, string categoria, string valor, CancellationToken ct)
    {
        var response = await _client.PutAsJsonAsync(RutaEtiqueta(id, categoria), PayloadValor(valor), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que AsignarEtiqueta funcione");
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

    private async Task IniciarVinculacionAsync(
        string numeroIdentificacion, DateOnly fechaInicio, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/colaboradores/{ComputarStreamId(numeroIdentificacion)}/vinculaciones",
            PayloadIniciarVinculacion(NuevoCodigoColaborador(), fechaInicio),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que IniciarVinculacion funcione");
    }

    private Task<HttpResponseMessage> RetirarEtiquetaAsync(
        string id, string categoria, CancellationToken ct) =>
        _client.DeleteAsync(RutaEtiqueta(id, categoria), ct);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // "área" retira lo asignado como "Area": es la misma categoria normalizada.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna204YPersisteEtiquetaRetirada_CuandoLaRutaLlegaConCategoriaEnOtraForma()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 15), ct);
        await AsignarEtiquetaAsync(id, "Area", "Ventas", ct);

        var response = await RetirarEtiquetaAsync(id, "área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaRetirada, Timeout,
            campoJson: "CategoriaNormalizada", valorJson: "area");

        existe.Should().BeTrue(
            $"el evento {TipoEventoEtiquetaRetirada} con CategoriaNormalizada 'area' deberia existir en el stream {id}");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna204SinEvento_CuandoCategoriaNoExiste()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 1), ct);

        var response = await RetirarEtiquetaAsync(id, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var existeRetiro = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaRetirada, TimeoutAusencia);
        existeRetiro.Should().BeFalse(
            "retirar una categoria sin etiqueta es un no-op exitoso -- no deberia persistir etiqueta_retirada");
    }

    // Estado ya alcanzado (MEF-ADR-0004): el segundo DELETE es un no-op y no agrega otro evento.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna204SinEvento_CuandoLaCategoriaYaFueRetirada()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 3, 1), ct);
        await AsignarEtiquetaAsync(id, "Area", "Ventas", ct);

        var primerRetiro = await RetirarEtiquetaAsync(id, "Área", ct);
        primerRetiro.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var segundoRetiro = await RetirarEtiquetaAsync(id, "Área", ct);

        segundoRetiro.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await segundoRetiro.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        var retiros = await postgres.ContarEventosAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaRetirada);
        retiros.Should().Be(1,
            "el segundo retiro es un no-op exitoso -- no deberia agregar un etiqueta_retirada mas");
    }

    // "Aera" no es "Area": un error de transcripcion NO aflora, da 204 sin evento y deja la etiqueta
    // existente intacta. Decision deliberada, no un descuido.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna204SinEvento_CuandoHayUnErrorDeTranscripcionEnLaCategoriaDeLaRuta()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 5), ct);
        await AsignarEtiquetaAsync(id, "Area", "Ventas", ct);

        var response = await RetirarEtiquetaAsync(id, "Aera", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var existeRetiro = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaRetirada, TimeoutAusencia);
        existeRetiro.Should().BeFalse(
            "un error de transcripcion en la categoria no deberia persistir un etiqueta_retirada");

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaAsignada);
        asignaciones.Should().Be(1,
            "la etiqueta original ('Area') deberia quedar intacta -- el no-op no la toca");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna409_CuandoUltimaVinculacionTieneTerminacionRegistrada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 10), ct);
        await AsignarEtiquetaAsync(id, "Área", "Ventas", ct);
        await TerminarVinculacionAsync(id, codigo, new DateOnly(2026, 6, 1), ct);

        var response = await RetirarEtiquetaAsync(id, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // Un preaviso futuro bloquea igual: las etiquetas describen la relacion laboral ACTIVA.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna409_CuandoTerminacionEsUnPreavisoConFechaFutura()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);
        var fechaPreavisoFutura = new DateOnly(2030, 12, 31);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 1), ct);
        await AsignarEtiquetaAsync(id, "Área", "Ventas", ct);
        await TerminarVinculacionAsync(id, codigo, fechaPreavisoFutura, ct);

        var response = await RetirarEtiquetaAsync(id, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // El reingreso nace limpio: la vigente no hereda la etiqueta de la vinculacion anterior, asi que
    // retirarla es un no-op.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna204SinEvento_CuandoEtiquetaPerteneceALaVinculacionAnteriorTrasReingreso()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 10), ct);
        await AsignarEtiquetaAsync(id, "Área", "Ventas", ct);
        await TerminarVinculacionAsync(id, codigo, new DateOnly(2026, 6, 1), ct);
        await IniciarVinculacionAsync(numeroIdentificacion, new DateOnly(2026, 7, 1), ct);

        var response = await RetirarEtiquetaAsync(id, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var existeRetiro = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaRetirada, TimeoutAusencia);
        existeRetiro.Should().BeFalse(
            "la etiqueta de la vinculacion anterior no existe en la vigente: el retiro es un no-op, sin evento nuevo");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        var response = await RetirarEtiquetaAsync(id, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var ct = TestContext.Current.CancellationToken;
        var idSinGuion = NuevoNumeroIdentificacion();

        var response = await RetirarEtiquetaAsync(idSinGuion, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna400_CuandoTipoDeLaIdentificacionEnLaRutaNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = $"XX-{NuevoNumeroIdentificacion()}";

        var response = await RetirarEtiquetaAsync(id, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna400_CuandoNumeroDeLaIdentificacionEnLaRutaQuedaVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        const string idConNumeroVacio = "CC-";

        var response = await RetirarEtiquetaAsync(idConNumeroVacio, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
