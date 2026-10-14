// Issue #373: smoke tests de ListarFichasColaborador, verbo QUERY (RFC 10008, MEF-ADR-0042) sobre
// colaboradores/fichas -- mismo recurso que ObtenerFichaColaborador (#356), pero el listado EXCLUYE
// no-vigentes a la fecha de referencia y agrega filtro AND por etiquetas + paginacion keyset. No hay
// proyeccion ni read model nuevos (issue hermano de #356): esta clase consulta la MISMA vista
// materializada FichaColaborador via (a') session.Query<FichaColaborador>().
//
// Arrange via API, nunca sembrando el event store por fuera de ella: cada colaborador se crea con
// POST Colaboradores (#330), se etiqueta con PUT colaboradores/{id}/etiquetas/{categoria} (#355,
// migrado de POST Colaboradores/Etiquetas a esta ruta por el issue #376) y se termina con POST
// colaboradores/{id}/vinculaciones/{codigo}:terminar (#349/#379) -- los mismos comandos que la
// proyeccion consume.
//
// Lifecycle Async (MEF-ADR-0034 seccion 3): el worker materializa/actualiza FichaColaborador
// DESPUES de que Colaboradores persiste sus eventos. Los casos de exito envuelven la consulta en
// Polling.WaitUntilAsync (timeout estandar 30s) -- unica excepcion documentada al "no usar Polling
// directo en tests".
//
// Aislamiento de datos SIN cleanup, en un entorno que ACUMULA fichas de corridas anteriores (todas
// las suites de este dominio registran colaboradores con NombreCompleto constante "[TEST] Smoke" y
// jamas los borran): un listado sin filtro nunca es un oraculo confiable por si solo. Cada test de
// aqui aisla SU propia fila de cualquier ruido historico con uno de estos dos mecanismos:
//   (1) Un CodigoSede UNICO (un Guid embebido, vease NuevaSedeUnica) asignado al registrar -- el
//       filtro plano por sede aisla la fila sin necesidad de ninguna etiqueta. Reemplaza desde #1000
//       al cursor posicionado a mano en el nombre, imposible con el cursor opaco (CA-ADR-0039).
//   (2) Un par de etiqueta discriminador con un Guid embebido en el VALOR -- nadie mas, en ninguna
//       corrida pasada o futura, puede tener ese valor exacto.
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
        IReadOnlyDictionary<string, string> EtiquetasNormalizadas,
        string? CodigoSede = null,
        Guid? JornadaId = null);

    private sealed record ListaSmoke(List<FichaColaboradorRespuestaSmoke> Elementos, string? SiguienteCursor);

    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Apellido con un Guid embebido -- garantiza que el NombreCompleto resultante ("[TEST]
    // {apellido}") es unico frente a cualquier ficha creada en cualquier corrida, pasada o futura,
    // de cualquier archivo de este dominio (todos los demas usan el literal constante "Smoke").
    private static string NuevoApellidoUnico(string prefijo) => $"{prefijo}-{Guid.CreateVersion7():N}";

    private static string NombreCompletoDe(string apellido) => $"[TEST] {apellido}";

    private static string NuevaSedeUnica() => $"SEDE-1000-{Guid.CreateVersion7():N}";

    // Mismo formato que ColaboradorAggregateRoot.ComputarStreamId (separador "-" desde #381),
    // reconstruido localmente (oraculo independiente, MEF-ADR-0002): el smoke test no referencia el
    // Function App.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    // Ruta migrada por el issue #376 (mismo template que Asignar/RetirarEtiquetaSmokeTests): la
    // categoria viaja cruda en la URL (puede traer tildes) y se URL-encodea al construir la ruta.
    private static string RutaEtiqueta(string id, string categoria) =>
        $"/api/colaboradores/{id}/etiquetas/{Uri.EscapeDataString(categoria)}";

    // Filtro por sede unica y sin etiquetas -- el mecanismo de aislamiento (1) del encabezado.
    private static object FiltroPorSede(
        DateOnly fechaReferencia, string codigoSede, int? take = null, string? cursor = null) => new
        {
            fechaReferencia,
            etiquetas = (object?)null,
            cursor,
            take,
            codigoSede
        };

    // Filtro AND por etiquetas, sin cursor (mecanismo de aislamiento (2) del encabezado: al menos
    // una de las etiquetas trae un Guid embebido en el valor, unico para la corrida).
    private static object FiltroPorEtiquetas(
        DateOnly fechaReferencia, params (string Categoria, string Valor)[] etiquetas) => new
        {
            fechaReferencia,
            etiquetas = etiquetas.Select(e => new { categoria = e.Categoria, valor = e.Valor }).ToArray()
        };

    // Filtro combinado (issue #519 CA-4): una etiqueta discriminadora de la corrida MAS la sede
    // opcional -- ejercita el AND real entre el containment JSONB de etiquetas y la igualdad plana
    // por sede. Con codigoSede null el filtro es exactamente el de antes de #519 (no filtra por
    // sede), que es la segunda mitad de CA-4.
    private static object FiltroPorEtiquetaYSede(
        DateOnly fechaReferencia, (string Categoria, string Valor) etiqueta, string? codigoSede) => new
        {
            fechaReferencia,
            etiquetas = new[] { new { categoria = etiqueta.Categoria, valor = etiqueta.Valor } },
            codigoSede
        };

    // Arrange comun: registra un colaborador con una vinculacion abierta -- via el comando que la
    // origina (#330), nunca sembrando el event store por fuera del API. codigoSede es opcional
    // (issue #519): RegistrarColaborador lo reenvia tal cual al VinculacionIniciada inicial, que la
    // proyeccion asienta en FichaColaborador.CodigoSede.
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

    // Arrange comun (CA-1): cierra la vinculacion vigente -- via el comando que la origina
    // (#349/#379). Issue #379: la ruta gano el {codigo} -- ya no es
    // "/api/Colaboradores/Terminaciones" con identificacion en el body.
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

    // Arrange comun (CA-2): asigna una etiqueta dinamica -- via el comando que la origina (#355),
    // migrado a PUT colaboradores/{id}/etiquetas/{categoria} por el issue #376.
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
    // vinculacion abierta): Etiquetas: null no aplica ningun containment -- la sede unica aisla la
    // fila sin depender de ninguna etiqueta.
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

    // CA-3 (paginacion keyset) + CA-6 (gate) + #519 CA-5 (convive con el filtro por sede) + #1000
    // CA-2/CA-6: recorrido de dos paginas con el siguienteCursor opaco del servidor. Verifica end-to-end
    // que Marten traduce el predicado compuesto CompareTo sobre strings contra el Postgres real de dev.
    // La sede unica acota el filtro a A y B: la pagina 1 es A con cursor y la 2 es B sin cursor.
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

    // #1000 CA-4: un cursor que no salio del servidor, tambien el objeto de la forma vieja -> 422.
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

        var lista = await response.Content.ReadFromJsonAsync<ListaSmoke>(JsonOptions, cancellationToken: ct);
        lista!.Elementos.Should().BeEmpty();
        lista.SiguienteCursor.Should().BeNull();
    }

    // Issue #519 CA-4: el filtro por sede es un Where mas del mismo AND -- con la MISMA etiqueta
    // discriminadora de la corrida, agregar la sede deja solo al colaborador de esa sede; quitarla
    // devuelve a los dos (el comportamiento previo a #519 no cambia). El codigo de sede lleva un
    // Guid embebido: nadie mas, en ninguna corrida pasada o futura, puede tener esa sede exacta.
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

        // Sin filtro de sede: los dos colaboradores de esta corrida (el comportamiento de antes de
        // #519). Es ademas el punto de sincronizacion -- confirma que la proyeccion ya materializo
        // ambas fichas antes de afirmar sobre la exclusion de abajo.
        var sinFiltroDeSede = await ConsultarHastaQueAsync(
            FiltroPorEtiquetaYSede(fechaReferencia, (categoriaCorrida, corridaId), codigoSede: null),
            lista => lista.Any(f => f.Id == idConSede) && lista.Any(f => f.Id == idSinSede),
            ct);

        sinFiltroDeSede.Should().HaveCount(2, "sin CodigoSede el filtro no cambia respecto de #373");

        // Con filtro de sede: solo el colaborador de esa sede. La sincronizacion ya la garantizo el
        // assert anterior, asi que un resultado incompleto aqui seria un fallo real del filtro.
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

        var porJornada = await ConsultarHastaQueAsync(
            new { fechaReferencia, jornadaId },
            lista => lista.Any(f => f.Id == idConJornada),
            ct);

        porJornada.Should().ContainSingle().Which.Id.Should().Be(idConJornada);
        porJornada.Single().JornadaId.Should().Be(jornadaId);

        var porJornadaYSede = await ConsultarListaAsync(new { fechaReferencia, jornadaId, codigoSede }, ct);
        porJornadaYSede.Should().ContainSingle(f => f.Id == idConJornada,
            "jornadaId se combina en AND con codigoSede");

        var otraJornada = await ConsultarListaAsync(
            new { fechaReferencia, jornadaId = Guid.CreateVersion7(), codigoSede }, ct);
        otraJornada.Should().BeEmpty("ninguna ficha tiene esa Jornada");
        otraJornada.Should().NotContain(f => f.Id == idSinJornada);
    }
}
