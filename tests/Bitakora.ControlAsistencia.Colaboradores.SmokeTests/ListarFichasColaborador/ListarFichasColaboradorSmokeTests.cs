using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.ListarFichasColaborador;

public class ListarFichasColaboradorSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaListado = "/api/colaboradores/fichas";
    private const string RutaRegistrar = "/api/Colaboradores";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly HttpMethod MetodoQuery = new("QUERY");

    // Case-insensitive: la respuesta viaja en camelCase (ComposicionServicios configura
    // JsonNamingPolicy.CamelCase), mientras que las formas locales de este archivo son PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Formas locales desacopladas del read model y del DTO de produccion: el smoke no referencia
    // ReadModels ni el Function App (MEF-ADR-0034 seccion 5); solo replican el shape JSON.
    private sealed record EtiquetaFichaSmoke(string Categoria, string Valor);

    private sealed record FichaColaboradorRespuestaSmoke(
        string Id,
        string NombreCompleto,
        string CodigoColaborador,
        DateOnly VigenteDesde,
        DateOnly? VigenteHasta,
        IReadOnlyList<EtiquetaFichaSmoke> Etiquetas,
        IReadOnlyDictionary<string, string> EtiquetasNormalizadas,
        string? CodigoSede = null,
        Guid? JornadaId = null);

    private sealed record ListaSmoke(List<FichaColaboradorRespuestaSmoke> Elementos, string? SiguienteCursor);

    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Hace unico el NombreCompleto ("[TEST] {apellido}") frente al literal "Smoke" de las demas suites.
    private static string NuevoApellidoUnico(string prefijo) => $"{prefijo}-{Guid.CreateVersion7():N}";

    private static string NombreCompletoDe(string apellido) => $"[TEST] {apellido}";

    // Sin cleanup el entorno acumula fichas de todas las corridas: cada test aisla su fila con una sede
    // unica o con una etiqueta que lleva un Guid en el valor.
    private static string NuevaSedeUnica() => $"SEDE-1000-{Guid.CreateVersion7():N}";

    // Oraculo independiente (MEF-ADR-0002), reconstruido a mano: el smoke no referencia el Function App.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    // La categoria puede traer tildes y se escapa.
    private static string RutaEtiqueta(string id, string categoria) =>
        $"/api/colaboradores/{id}/etiquetas/{Uri.EscapeDataString(categoria)}";

    private static object FiltroPorSede(
        DateOnly fechaReferencia, string codigoSede, int? take = null, string? cursor = null) => new
        {
            fechaReferencia,
            etiquetas = (object?)null,
            cursor,
            take,
            codigoSede
        };

    // Al menos una etiqueta debe traer un Guid en el valor para aislar la corrida.
    private static object FiltroPorEtiquetas(
        DateOnly fechaReferencia, params (string Categoria, string Valor)[] etiquetas) => new
        {
            fechaReferencia,
            etiquetas = etiquetas.Select(e => new { categoria = e.Categoria, valor = e.Valor }).ToArray()
        };

    // Ejercita el AND entre el containment JSONB de etiquetas y la igualdad plana por sede; con
    // codigoSede null no filtra por sede.
    private static object FiltroPorEtiquetaYSede(
        DateOnly fechaReferencia, (string Categoria, string Valor) etiqueta, string? codigoSede) => new
        {
            fechaReferencia,
            etiquetas = new[] { new { categoria = etiqueta.Categoria, valor = etiqueta.Valor } },
            codigoSede
        };

    private async Task RegistrarColaboradorAsync(
        string numeroIdentificacion, DateOnly fechaInicio, string primerApellido,
        string codigoColaborador, CancellationToken ct, string? codigoSede = null)
    {
        var response = await _client.PostAsJsonAsync(RutaRegistrar, new
        {
            tipoIdentificacion = TipoIdentificacionCc,
            numeroIdentificacion,
            primerNombre = "[TEST]",
            segundoNombre = (string?)null,
            primerApellido,
            segundoApellido = (string?)null,
            codigoColaborador,
            fechaInicio,
            codigoSede
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que RegistrarColaborador funcione");
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

    private async Task AsignarEtiquetaAsync(
        string numeroIdentificacion, string categoria, string valor, CancellationToken ct)
    {
        var id = ComputarStreamId(numeroIdentificacion);
        var response = await _client.PutAsJsonAsync(RutaEtiqueta(id, categoria), new { valor }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que AsignarEtiqueta funcione");
    }

    private Task<HttpResponseMessage> ConsultarAsync(object filtro, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(MetodoQuery, RutaListado)
        {
            Content = JsonContent.Create(filtro)
        };
        return _client.SendAsync(request, ct);
    }

    private async Task<ListaSmoke> ConsultarSobreAsync(object filtro, CancellationToken ct)
    {
        var response = await ConsultarAsync(filtro, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var lista = await response.Content.ReadFromJsonAsync<ListaSmoke>(JsonOptions, cancellationToken: ct);
        return lista!;
    }

    private async Task<List<FichaColaboradorRespuestaSmoke>> ConsultarListaAsync(
        object filtro, CancellationToken ct) => (await ConsultarSobreAsync(filtro, ct)).Elementos;

    // La proyeccion es Async (MEF-ADR-0034 seccion 3): un timeout agotado es un fallo real (worker no
    // desplegado, o el arrange nunca se materializo).
    private Task<List<FichaColaboradorRespuestaSmoke>> ConsultarHastaQueAsync(
        object filtro, Func<List<FichaColaboradorRespuestaSmoke>, bool> condicion, CancellationToken ct) =>
        Polling.WaitUntilAsync(async () =>
        {
            var lista = await ConsultarListaAsync(filtro, ct);
            return condicion(lista) ? lista : null;
        }, Timeout);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Vigencia inclusive. Primero se espera VigenteHasta == fechaEfectiva (la terminacion ya proyectada,
    // no el centinela); despues, sin polling, al dia siguiente la ficha ya no aparece.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_IncluyeElDiaEfectivoDeTerminacionYLoExcluyeAlDiaSiguiente_CuandoLaVigenciaEsInclusive()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var streamId = ComputarStreamId(numeroIdentificacion);
        var fechaInicio = new DateOnly(2026, 3, 1);
        var fechaEfectiva = new DateOnly(2026, 6, 30);
        var apellido = NuevoApellidoUnico("Vigencia373");
        var codigoSede = NuevaSedeUnica();

        var codigoColaborador = NuevoCodigoColaborador();
        await RegistrarColaboradorAsync(
            numeroIdentificacion, fechaInicio, apellido, codigoColaborador, ct, codigoSede);
        await TerminarVinculacionAsync(streamId, codigoColaborador, fechaEfectiva, ct);

        var paginaEnLaFechaEfectiva = await ConsultarHastaQueAsync(
            FiltroPorSede(fechaEfectiva, codigoSede),
            lista => lista.Any(f => f.Id == streamId && f.VigenteHasta == fechaEfectiva),
            ct);

        paginaEnLaFechaEfectiva.Should().ContainSingle(f => f.Id == streamId,
            "el dia efectivo de terminacion es el ULTIMO dia vigente, inclusive (CA-1)");

        // Deterministico: la proyeccion ya confirmo estar al dia en el assert anterior.
        var paginaAlDiaSiguiente = await ConsultarListaAsync(
            FiltroPorSede(fechaEfectiva.AddDays(1), codigoSede), ct);

        paginaAlDiaSiguiente.Should().NotContain(f => f.Id == streamId,
            "la vinculacion terminada desaparece del listado al dia siguiente de su fecha efectiva, sin ningun evento nuevo (CA-1)");
    }

    // Etiqueta.Crear normaliza ambos lados por igual. El Guid en el valor de la etiqueta de la corrida
    // hace inequivoca la fila.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_AplicaAndDeEtiquetasConNormalizacionSimetrica_CuandoElFiltroUsaFormasDistintasDeLasAsignadas()
    {
        var ct = TestContext.Current.CancellationToken;
        var fechaReferencia = new DateOnly(2026, 4, 20);
        var categoriaCorrida = "SmokeCorrida373";
        var corridaId = Guid.CreateVersion7().ToString("N");
        var apellido = NuevoApellidoUnico("Etiquetas373");
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var streamId = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(
            numeroIdentificacion, new DateOnly(2026, 1, 1), apellido, NuevoCodigoColaborador(), ct);
        await AsignarEtiquetaAsync(numeroIdentificacion, categoriaCorrida, corridaId, ct);
        await AsignarEtiquetaAsync(numeroIdentificacion, "Área", "Tecnología", ct);
        await AsignarEtiquetaAsync(numeroIdentificacion, "cargo", "desarrollador", ct);

        var filtroCoincide = FiltroPorEtiquetas(fechaReferencia,
            (categoriaCorrida, corridaId), ("área", "tecnología"), ("Cargo", "Desarrollador"));

        var pagina = await ConsultarHastaQueAsync(
            filtroCoincide, lista => lista.Any(f => f.Id == streamId), ct);

        pagina.Should().ContainSingle(f => f.Id == streamId,
            "'área'/'tecnología' y 'Cargo'/'Desarrollador' deberian normalizar a las mismas claves que 'Área'/'Tecnología' y 'cargo'/'desarrollador'");

        // AND estricto: cambiar SOLO un valor no debe encontrar nada; nadie mas tiene esa combinacion.
        var filtroSinCoincidencia = FiltroPorEtiquetas(fechaReferencia,
            (categoriaCorrida, corridaId), ("cargo", "contador"));

        var paginaSinCoincidencia = await ConsultarListaAsync(filtroSinCoincidencia, ct);

        paginaSinCoincidencia.Should().BeEmpty(
            "el AND estricto no deberia devolver un colaborador cuyo cargo es 'desarrollador', no 'contador'");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_IncluyeAlVigenteConVigenteHastaVacio_CuandoElFiltroNoTraeEtiquetas()
    {
        var ct = TestContext.Current.CancellationToken;
        var fechaReferencia = new DateOnly(2026, 5, 1);
        var apellido = NuevoApellidoUnico("SinEtiquetas373");
        var nombreCompleto = NombreCompletoDe(apellido);
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var streamId = ComputarStreamId(numeroIdentificacion);
        var codigoColaborador = NuevoCodigoColaborador();
        var codigoSede = NuevaSedeUnica();

        await RegistrarColaboradorAsync(
            numeroIdentificacion, new DateOnly(2026, 1, 1), apellido, codigoColaborador, ct, codigoSede);

        var pagina = await ConsultarHastaQueAsync(
            FiltroPorSede(fechaReferencia, codigoSede),
            lista => lista.Any(f => f.Id == streamId),
            ct);

        var ficha = pagina.Should().ContainSingle(f => f.Id == streamId).Subject;
        ficha.CodigoColaborador.Should().Be(codigoColaborador);
        ficha.NombreCompleto.Should().Be(nombreCompleto);
        ficha.VigenteHasta.Should().BeNull(
            "el centinela de vigencia abierta (9999-12-31) jamas debe salir por la API (misma regla que #356 CA-6)");
    }

    // Gate end-to-end del verbo QUERY (MEF-ADR-0042 seccion 6): falla si el host de dev no reenvia QUERY
    // o si Marten no traduce el CompareTo compuesto sobre strings; nunca es caso de Skip. La sede unica
    // acota el filtro a A y B.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_PaginaSinSaltarNiRepetir_CuandoSeUsaElSiguienteCursor()
    {
        var ct = TestContext.Current.CancellationToken;
        var fechaReferencia = new DateOnly(2026, 4, 1);
        var codigoSede = NuevaSedeUnica();
        var apellidoBase = NuevoApellidoUnico("Paginacion373");

        var numeroA = NuevoNumeroIdentificacion();
        var numeroB = NuevoNumeroIdentificacion();
        var idA = ComputarStreamId(numeroA);
        var idB = ComputarStreamId(numeroB);

        await RegistrarColaboradorAsync(
            numeroA, new DateOnly(2026, 1, 1), $"{apellidoBase}-A", NuevoCodigoColaborador(), ct, codigoSede);
        await RegistrarColaboradorAsync(
            numeroB, new DateOnly(2026, 1, 1), $"{apellidoBase}-B", NuevoCodigoColaborador(), ct, codigoSede);

        // Sin take: punto de sincronizacion -- ambas fichas materializadas y todo llega sin cursor.
        var todas = await Polling.WaitUntilAsync(async () =>
        {
            var lista = await ConsultarSobreAsync(FiltroPorSede(fechaReferencia, codigoSede), ct);
            return lista.Elementos.Count == 2 ? lista : null;
        }, Timeout);
        todas.SiguienteCursor.Should().BeNull("sin take el listado devuelve todas las fichas del filtro");

        var pagina1 = await ConsultarSobreAsync(FiltroPorSede(fechaReferencia, codigoSede, take: 1), ct);
        pagina1.Elementos.Should().ContainSingle(f => f.Id == idA);
        pagina1.SiguienteCursor.Should().NotBeNullOrEmpty("queda B por recorrer");

        var pagina2 = await ConsultarSobreAsync(
            FiltroPorSede(fechaReferencia, codigoSede, take: 1, cursor: pagina1.SiguienteCursor), ct);
        pagina2.Elementos.Should().ContainSingle(f => f.Id == idB,
            "el siguienteCursor de A deberia continuar exactamente en B, sin saltar ni repetir");
        pagina2.SiguienteCursor.Should().BeNull("B es la ultima ficha del filtro");
    }

    // El 415 se decide ANTES de leer el body.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_Retorna415_CuandoContentTypeNoEsJson()
    {
        var ct = TestContext.Current.CancellationToken;

        using var request = new HttpRequestMessage(MetodoQuery, RutaListado)
        {
            Content = new StringContent("{}", Encoding.UTF8, "text/plain")
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_Retorna400_CuandoElBodyEsJsonInvalido()
    {
        var ct = TestContext.Current.CancellationToken;

        using var request = new HttpRequestMessage(MetodoQuery, RutaListado)
        {
            Content = new StringContent("{ esto no es json valido", Encoding.UTF8, "application/json")
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_Retorna400_CuandoElBodyEstaVacio()
    {
        var ct = TestContext.Current.CancellationToken;

        using var request = new HttpRequestMessage(MetodoQuery, RutaListado)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // FechaReferencia es obligatoria: el back jamas resuelve "hoy".
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_Retorna422_CuandoFechaReferenciaNoLlega()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(new { }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_Retorna422_CuandoUnaEtiquetaDelFiltroEsInvalida()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = new
        {
            fechaReferencia = new DateOnly(2026, 1, 1),
            etiquetas = new[] { new { categoria = "", valor = "Tecnologia" } }
        };

        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // Un cursor que no salio del servidor -> 422; el siguiente test cubre el objeto de la forma vieja.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_Retorna422_CuandoElCursorNoEsValido()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = new { fechaReferencia = new DateOnly(2026, 1, 1), cursor = "no-es-un-cursor" };

        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_Retorna422_CuandoElCursorTraeLaFormaVieja()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = new
        {
            fechaReferencia = new DateOnly(2026, 1, 1),
            cursor = new { nombreCompleto = "[TEST] Algo", id = "CC-1" }
        };

        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_Retorna200ConListaVacia_CuandoNingunVigenteCumpleElFiltroDeEtiquetas()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = FiltroPorEtiquetas(new DateOnly(2026, 1, 1),
            ("smoke-inexistente-373", Guid.CreateVersion7().ToString("N")));

        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var lista = await response.Content.ReadFromJsonAsync<ListaSmoke>(JsonOptions, cancellationToken: ct);
        lista!.Elementos.Should().BeEmpty();
        lista.SiguienteCursor.Should().BeNull();
    }

    // Con la MISMA etiqueta de la corrida, agregar la sede deja solo al colaborador de esa sede y quitarla
    // devuelve a los dos.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_DevuelveSoloLasFichasDeEsaSede_CuandoElFiltroTraeCodigoSede()
    {
        var ct = TestContext.Current.CancellationToken;
        var fechaReferencia = new DateOnly(2026, 5, 15);
        var categoriaCorrida = "SmokeCorrida519";
        var corridaId = Guid.CreateVersion7().ToString("N");
        var codigoSede = $"SEDE-519-{Guid.CreateVersion7():N}";

        var numeroConSede = NuevoNumeroIdentificacion();
        var numeroSinSede = NuevoNumeroIdentificacion();
        var idConSede = ComputarStreamId(numeroConSede);
        var idSinSede = ComputarStreamId(numeroSinSede);

        await RegistrarColaboradorAsync(
            numeroConSede, new DateOnly(2026, 1, 1), NuevoApellidoUnico("ConSede519"),
            NuevoCodigoColaborador(), ct, codigoSede);
        await RegistrarColaboradorAsync(
            numeroSinSede, new DateOnly(2026, 1, 1), NuevoApellidoUnico("SinSede519"),
            NuevoCodigoColaborador(), ct);
        await AsignarEtiquetaAsync(numeroConSede, categoriaCorrida, corridaId, ct);
        await AsignarEtiquetaAsync(numeroSinSede, categoriaCorrida, corridaId, ct);

        // Punto de sincronizacion: confirma que ambas fichas estan materializadas antes de afirmar la
        // exclusion de abajo.
        var sinFiltroDeSede = await ConsultarHastaQueAsync(
            FiltroPorEtiquetaYSede(fechaReferencia, (categoriaCorrida, corridaId), codigoSede: null),
            lista => lista.Any(f => f.Id == idConSede) && lista.Any(f => f.Id == idSinSede),
            ct);

        sinFiltroDeSede.Should().HaveCount(2, "sin CodigoSede el filtro no cambia respecto de #373");

        var conFiltroDeSede = await ConsultarHastaQueAsync(
            FiltroPorEtiquetaYSede(fechaReferencia, (categoriaCorrida, corridaId), codigoSede),
            lista => lista.Any(f => f.Id == idConSede),
            ct);

        conFiltroDeSede.Should().ContainSingle(f => f.Id == idConSede,
            "el AND entre la etiqueta de la corrida y la sede deberia dejar solo al colaborador de esa sede");
        conFiltroDeSede.Should().NotContain(f => f.Id == idSinSede,
            "el colaborador sin sede no puede colarse en un filtro por sede");
        conFiltroDeSede.Single().CodigoSede.Should().Be(codigoSede,
            "la respuesta expone el codigo de sede de la ficha (issue #519 CA-3)");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_DevuelveSoloLaFichaDeLaJornada_CuandoElFiltroTraeJornadaId()
    {
        var ct = TestContext.Current.CancellationToken;
        var fechaReferencia = new DateOnly(2026, 5, 1);
        var jornadaId = Guid.CreateVersion7();
        var codigoSede = NuevaSedeUnica();
        var numeroConJornada = NuevoNumeroIdentificacion();
        var numeroSinJornada = NuevoNumeroIdentificacion();
        var idConJornada = ComputarStreamId(numeroConJornada);
        var idSinJornada = ComputarStreamId(numeroSinJornada);

        await RegistrarColaboradorAsync(
            numeroConJornada, new DateOnly(2026, 1, 1), NuevoApellidoUnico("ConJornada942"),
            NuevoCodigoColaborador(), ct, codigoSede);
        await RegistrarColaboradorAsync(
            numeroSinJornada, new DateOnly(2026, 1, 1), NuevoApellidoUnico("SinJornada942"),
            NuevoCodigoColaborador(), ct, codigoSede);

        var asignacion = await _client.PutAsJsonAsync($"/api/colaboradores/{idConJornada}/jornada", new { jornadaId }, ct);
        asignacion.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que AsignarJornada funcione");

        // Sincroniza ambas fichas: sin esto, la ausencia de la ficha sin Jornada en el AND de abajo
        // podria deberse a que la proyeccion aun no la materializo, no al filtro.
        await ConsultarHastaQueAsync(
            new { fechaReferencia, codigoSede },
            lista => lista.Any(f => f.Id == idConJornada) && lista.Any(f => f.Id == idSinJornada),
            ct);

        var porJornada = await ConsultarHastaQueAsync(
            new { fechaReferencia, jornadaId },
            lista => lista.Any(f => f.Id == idConJornada),
            ct);

        porJornada.Should().ContainSingle().Which.Id.Should().Be(idConJornada);
        porJornada.Single().JornadaId.Should().Be(jornadaId);

        var porJornadaYSede = await ConsultarListaAsync(new { fechaReferencia, jornadaId, codigoSede }, ct);
        porJornadaYSede.Should().ContainSingle("jornadaId se combina en AND con codigoSede")
            .Which.Id.Should().Be(idConJornada);

        var otraJornada = await ConsultarListaAsync(
            new { fechaReferencia, jornadaId = Guid.CreateVersion7(), codigoSede }, ct);
        otraJornada.Should().BeEmpty("ninguna ficha de la sede tiene esa Jornada");
    }
}
