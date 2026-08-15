// Issue #373: smoke tests de ListarFichasColaborador, verbo QUERY (RFC 10008, MEF-ADR-0042) sobre
// colaboradores/fichas -- mismo recurso que ObtenerFichaColaborador (#356), pero el listado EXCLUYE
// no-vigentes a la fecha de referencia y agrega filtro AND por etiquetas + paginacion keyset. No hay
// proyeccion ni read model nuevos (issue hermano de #356): esta clase consulta la MISMA vista
// materializada FichaColaborador via (a') session.Query<FichaColaborador>().
//
// Arrange via API, nunca sembrando el event store por fuera de ella: cada colaborador se crea con
// POST Colaboradores (#330), se etiqueta con POST Colaboradores/Etiquetas (#355) y se termina con
// POST Colaboradores/Terminaciones (#349) -- los mismos comandos que la proyeccion consume.
//
// Lifecycle Async (MEF-ADR-0034 seccion 3): el worker materializa/actualiza FichaColaborador
// DESPUES de que Colaboradores persiste sus eventos. Los casos de exito envuelven la consulta en
// Polling.WaitUntilAsync (timeout estandar 30s) -- unica excepcion documentada al "no usar Polling
// directo en tests".
//
// Aislamiento de datos SIN cleanup, en un entorno que ACUMULA fichas de corridas anteriores (todas
// las suites de este dominio registran colaboradores con NombreCompleto constante "[TEST] Smoke" y
// jamas los borran): un listado sin filtro nunca es un oraculo confiable por si solo. Cada test de
// aqui aisla SU propia fila de cualquier ruido historico con uno de estos dos mecanismos,
// combinables con el paginado real del endpoint:
//   (1) NombreCompleto UNICO (un Guid embebido en el apellido, vease NuevoApellidoUnico) + Cursor
//       posicionado exactamente en ese nombre (con Id vacio, que ordena antes que cualquier Id real)
//       -- aisla la fila sin necesidad de ninguna etiqueta.
//   (2) Un par de etiqueta discriminador con un Guid embebido en el VALOR -- nadie mas, en ninguna
//       corrida pasada o futura, puede tener ese valor exacto.
// Ambos duplican, a proposito, el mecanismo real de paginacion keyset que CA-3/CA-6 exigen probar
// contra el entorno real: no es un atajo de test, es la MISMA superficie publica.
//
// CA-6 (gate, MEF-ADR-0042 seccion 6): el primer verbo QUERY del consumidor. El test de paginacion
// (ListarFichasColaborador_PaginaSinSaltarNiRepetir...) es el gate end-to-end: si el host de dev no
// reenvia el verbo QUERY, o si Marten no traduce el predicado compuesto CompareTo sobre strings, ese
// test falla -- nunca es un caso para Assert.Skip (fallo real, no ambiental). La colacion de la base
// de dev (en_US.utf8 vs C, ver MEF-ADR-0042 seccion 6) no es observable via HTTP: se registra
// manualmente en el resumen de esta corrida, fuera de este archivo.
using System.Net;
using System.Net.Http.Json;
using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.ListarFichasColaborador;

public class ListarFichasColaboradorSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaListado = "/api/colaboradores/fichas";
    private const string RutaRegistrar = "/api/Colaboradores";
    private const string RutaTerminaciones = "/api/Colaboradores/Terminaciones";
    private const string RutaEtiquetas = "/api/Colaboradores/Etiquetas";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly HttpMethod MetodoQuery = new("QUERY");

    // Case-insensitive: la respuesta viaja en camelCase (ComposicionServicios configura
    // JsonNamingPolicy.CamelCase), mientras que las formas locales de este archivo son PascalCase.
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Formas locales DESACOPLADAS del read model de produccion (ReadModels.Colaboradores) y del DTO
    // de respuesta de ObtenerFichaColaborador: replican solo el shape JSON de la respuesta HTTP de
    // ESTE endpoint. El smoke test no referencia ReadModels ni el Function App (isla, MEF-ADR-0034
    // seccion 5).
    private sealed record EtiquetaFichaSmoke(string Categoria, string Valor);

    private sealed record FichaColaboradorRespuestaSmoke(
        string Id,
        string NombreCompleto,
        string CodigoColaborador,
        DateOnly VigenteDesde,
        DateOnly? VigenteHasta,
        IReadOnlyList<EtiquetaFichaSmoke> Etiquetas,
        IReadOnlyDictionary<string, string> EtiquetasNormalizadas);

    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    private static string NuevoCodigoColaborador() => $"[TEST]-{Guid.CreateVersion7()}";

    // Apellido con un Guid embebido -- garantiza que el NombreCompleto resultante ("[TEST]
    // {apellido}") es unico frente a cualquier ficha creada en cualquier corrida, pasada o futura,
    // de cualquier archivo de este dominio (todos los demas usan el literal constante "Smoke").
    private static string NuevoApellidoUnico(string prefijo) => $"{prefijo}-{Guid.CreateVersion7():N}";

    private static string NombreCompletoDe(string apellido) => $"[TEST] {apellido}";

    // Mismo formato que ColaboradorAggregateRoot.ComputarStreamId (separador "-" desde #381),
    // reconstruido localmente (oraculo independiente, MEF-ADR-0002): el smoke test no referencia el
    // Function App.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    // Filtro con Cursor explicito y sin etiquetas -- el mecanismo de aislamiento (1) del encabezado:
    // posiciona el listado exactamente en cursorNombre (con cursorId vacio, que ordena antes que
    // cualquier Id real no vacio), aislando la fila de interes del resto del dataset acumulado.
    private static object FiltroPorCursor(
        DateOnly fechaReferencia, string cursorNombre, string cursorId, int take = 1) => new
        {
            fechaReferencia,
            etiquetas = (object?)null,
            cursor = new { nombreCompleto = cursorNombre, id = cursorId },
            take
        };

    // Filtro AND por etiquetas, sin cursor (mecanismo de aislamiento (2) del encabezado: al menos
    // una de las etiquetas trae un Guid embebido en el valor, unico para la corrida).
    private static object FiltroPorEtiquetas(
        DateOnly fechaReferencia, params (string Categoria, string Valor)[] etiquetas) => new
        {
            fechaReferencia,
            etiquetas = etiquetas.Select(e => new { categoria = e.Categoria, valor = e.Valor }).ToArray(),
            cursor = (object?)null,
            take = 50
        };

    // Arrange comun: registra un colaborador con una vinculacion abierta -- via el comando que la
    // origina (#330), nunca sembrando el event store por fuera del API.
    private async Task RegistrarColaboradorAsync(
        string numeroIdentificacion, DateOnly fechaInicio, string primerApellido,
        string codigoColaborador, CancellationToken ct)
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
            fechaInicio
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que RegistrarColaborador funcione");
    }

    // Arrange comun (CA-1): cierra la vinculacion vigente -- via el comando que la origina (#349).
    private async Task TerminarVinculacionAsync(
        string numeroIdentificacion, DateOnly fechaEfectiva, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(RutaTerminaciones, new
        {
            tipoIdentificacion = TipoIdentificacionCc,
            numeroIdentificacion,
            fechaEfectiva
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que TerminarVinculacion funcione");
    }

    // Arrange comun (CA-2): asigna una etiqueta dinamica -- via el comando que la origina (#355).
    private async Task AsignarEtiquetaAsync(
        string numeroIdentificacion, string categoria, string valor, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(RutaEtiquetas, new
        {
            tipoIdentificacion = TipoIdentificacionCc,
            numeroIdentificacion,
            categoria,
            valor
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
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

    private async Task<List<FichaColaboradorRespuestaSmoke>> ConsultarListaAsync(
        object filtro, CancellationToken ct)
    {
        var response = await ConsultarAsync(filtro, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var lista = await response.Content.ReadFromJsonAsync<List<FichaColaboradorRespuestaSmoke>>(
            JsonOptions, cancellationToken: ct);
        return lista ?? [];
    }

    // Reintenta la consulta hasta que la proyeccion asincrona satisfaga la condicion -- unica
    // excepcion documentada al "no usar Polling directo en tests" (lifecycle Async, MEF-ADR-0034
    // seccion 3). Si el timeout se agota es un fallo real (worker no desplegado, o el efecto del
    // arrange nunca se materializo), Polling.WaitUntilAsync lanza TimeoutException.
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

    // CA-1 (vigencia inclusive): el dia efectivo de terminacion es el ULTIMO dia vigente. Se
    // confirma primero que la proyeccion ya aplico VinculacionTerminada (VigenteHasta == fechaEfectiva
    // exacto, no el centinela todavia sin procesar) y LUEGO -- de forma deterministica, sin polling,
    // porque la proyeccion ya esta al dia -- que al dia siguiente, sin ningun evento nuevo, la misma
    // ficha desaparece del listado.
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
        var nombreCompleto = NombreCompletoDe(apellido);

        await RegistrarColaboradorAsync(
            numeroIdentificacion, fechaInicio, apellido, NuevoCodigoColaborador(), ct);
        await TerminarVinculacionAsync(numeroIdentificacion, fechaEfectiva, ct);

        var paginaEnLaFechaEfectiva = await ConsultarHastaQueAsync(
            FiltroPorCursor(fechaEfectiva, nombreCompleto, cursorId: ""),
            lista => lista.Any(f => f.Id == streamId && f.VigenteHasta == fechaEfectiva),
            ct);

        paginaEnLaFechaEfectiva.Should().ContainSingle(f => f.Id == streamId,
            "el dia efectivo de terminacion es el ULTIMO dia vigente, inclusive (CA-1)");

        // Deterministico: la proyeccion ya confirmo estar al dia en el assert anterior.
        var paginaAlDiaSiguiente = await ConsultarListaAsync(
            FiltroPorCursor(fechaEfectiva.AddDays(1), nombreCompleto, cursorId: ""), ct);

        paginaAlDiaSiguiente.Should().NotContain(f => f.Id == streamId,
            "la vinculacion terminada desaparece del listado al dia siguiente de su fecha efectiva, sin ningun evento nuevo (CA-1)");
    }

    // CA-2 (filtro AND + normalizacion simetrica): filtrando con formas distintas (mayusculas,
    // tildes) a las asignadas, el colaborador SI aparece -- Etiqueta.Crear normaliza ambos lados por
    // igual (Tell-don't-Ask, MEF-ADR-0012). La categoria discriminadora lleva un Guid en el valor:
    // si el colaborador aparece, es EL, sin ambiguedad con ninguna otra corrida.
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

        // AND estricto: cambiar SOLO el valor de una categoria (misma corridaId, unica) no debe
        // encontrar nada -- nadie mas puede tener esa combinacion exacta.
        var filtroSinCoincidencia = FiltroPorEtiquetas(fechaReferencia,
            (categoriaCorrida, corridaId), ("cargo", "contador"));

        var paginaSinCoincidencia = await ConsultarListaAsync(filtroSinCoincidencia, ct);

        paginaSinCoincidencia.Should().BeEmpty(
            "el AND estricto no deberia devolver un colaborador cuyo cargo es 'desarrollador', no 'contador'");
    }

    // CA-2 (sin filtro de etiquetas retorna todos los vigentes) + CA-4 (VigenteHasta vacio en
    // vinculacion abierta): Etiquetas: null no aplica ningun containment -- el cursor posicionado en
    // el propio NombreCompleto (unico) aisla la fila sin depender de ninguna etiqueta.
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

        await RegistrarColaboradorAsync(
            numeroIdentificacion, new DateOnly(2026, 1, 1), apellido, codigoColaborador, ct);

        var pagina = await ConsultarHastaQueAsync(
            FiltroPorCursor(fechaReferencia, nombreCompleto, cursorId: ""),
            lista => lista.Any(f => f.Id == streamId),
            ct);

        var ficha = pagina.Should().ContainSingle(f => f.Id == streamId).Subject;
        ficha.CodigoColaborador.Should().Be(codigoColaborador);
        ficha.NombreCompleto.Should().Be(nombreCompleto);
        ficha.VigenteHasta.Should().BeNull(
            "el centinela de vigencia abierta (9999-12-31) jamas debe salir por la API (misma regla que #356 CA-6)");
    }

    // CA-3 (paginacion keyset) + CA-6 (gate): el cursor de la ultima fila continua la pagina
    // siguiente sin saltar ni repetir. Verifica end-to-end que Marten traduce el predicado compuesto
    // CompareTo sobre strings contra el Postgres real de dev -- el NO VERIFICADO de
    // skills/projections/read-apis.md, cerrado para este dominio por spike propio del implementer.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_PaginaSinSaltarNiRepetir_CuandoSeUsaElCursorDeLaUltimaFila()
    {
        var ct = TestContext.Current.CancellationToken;
        var fechaReferencia = new DateOnly(2026, 4, 1);
        var apellidoBase = NuevoApellidoUnico("Paginacion373");
        var apellidoA = $"{apellidoBase}-A";
        var apellidoB = $"{apellidoBase}-B";
        var nombreA = NombreCompletoDe(apellidoA);

        var numeroA = NuevoNumeroIdentificacion();
        var numeroB = NuevoNumeroIdentificacion();
        var idA = ComputarStreamId(numeroA);
        var idB = ComputarStreamId(numeroB);

        await RegistrarColaboradorAsync(
            numeroA, new DateOnly(2026, 1, 1), apellidoA, NuevoCodigoColaborador(), ct);
        await RegistrarColaboradorAsync(
            numeroB, new DateOnly(2026, 1, 1), apellidoB, NuevoCodigoColaborador(), ct);

        // Pagina 1: cursor posicionado exactamente en el nombre de A (Id vacio ordena antes que
        // cualquier Id real) -- con Take: 1 trae solo a A.
        var pagina1 = await ConsultarHastaQueAsync(
            FiltroPorCursor(fechaReferencia, nombreA, cursorId: "", take: 1),
            lista => lista.Any(f => f.Id == idA),
            ct);

        pagina1.Should().ContainSingle(f => f.Id == idA,
            "el cursor posicionado en el nombre de A, con Id vacio, deberia traer solo a A");

        // Pagina 2: cursor REAL de la ultima fila de la pagina 1 (NombreCompleto, Id de A) -- debe
        // continuar exactamente en B, sin saltarlo ni repetir A.
        var pagina2 = await ConsultarHastaQueAsync(
            FiltroPorCursor(fechaReferencia, nombreA, cursorId: idA, take: 1),
            lista => lista.Any(f => f.Id == idB),
            ct);

        pagina2.Should().ContainSingle(f => f.Id == idB,
            "el cursor (NombreCompleto, Id) de A deberia continuar exactamente en B, sin saltar ni repetir");
    }

    // CA-3 (clamp del Take): Take: 0 se clampea a 1 en el servidor -- sin el clamp, un Take(0) crudo
    // de LINQ devolveria una lista vacia SIEMPRE, sin importar que el colaborador exista y sea
    // vigente.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_ClampeaTakeAMinimoUno_CuandoTakeLlegaEnCero()
    {
        var ct = TestContext.Current.CancellationToken;
        var fechaReferencia = new DateOnly(2026, 4, 15);
        var apellido = NuevoApellidoUnico("Clamp373");
        var nombreCompleto = NombreCompletoDe(apellido);
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var streamId = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(
            numeroIdentificacion, new DateOnly(2026, 1, 1), apellido, NuevoCodigoColaborador(), ct);

        var pagina = await ConsultarHastaQueAsync(
            FiltroPorCursor(fechaReferencia, nombreCompleto, cursorId: "", take: 0),
            lista => lista.Any(f => f.Id == streamId),
            ct);

        pagina.Should().ContainSingle(f => f.Id == streamId,
            "Take: 0 deberia clampearse a 1 en el servidor -- un Take(0) crudo nunca habria devuelto esta fila");
    }

    // CA-4: sin Content-Type: application/json -> 415, verificado ANTES de leer el body.
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

    // CA-4: body con Content-Type json pero sintacticamente invalido -> 400.
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

    // CA-4: body ausente (Content-Type json, cero bytes) -> 400.
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

    // CA-4: JSON valido sin FechaReferencia -> 422 (obligatoria; el back jamas resuelve "hoy").
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_Retorna422_CuandoFechaReferenciaNoLlega()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(new { }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // CA-4/CA-2: una etiqueta del filtro con categoria/valor vacios -> 422 (Etiqueta.Crear rechaza).
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

    // CA-4: cursor con un solo campo presente (falta Id) -> 422.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_Retorna422_CuandoElCursorLlegaIncompleto()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = new
        {
            fechaReferencia = new DateOnly(2026, 1, 1),
            cursor = new { nombreCompleto = "[TEST] Algo" } // sin id
        };

        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // CA-4: sin resultados -> 200 con lista vacia, nunca 404. Categoria/valor con un Guid embebido:
    // nadie pudo haber asignado jamas esta combinacion exacta.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasColaborador_Retorna200ConListaVacia_CuandoNingunVigenteCumpleElFiltroDeEtiquetas()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = FiltroPorEtiquetas(new DateOnly(2026, 1, 1),
            ("smoke-inexistente-373", Guid.CreateVersion7().ToString("N")));

        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var lista = await response.Content.ReadFromJsonAsync<List<FichaColaboradorRespuestaSmoke>>(
            JsonOptions, cancellationToken: ct);
        (lista ?? []).Should().BeEmpty();
    }
}
