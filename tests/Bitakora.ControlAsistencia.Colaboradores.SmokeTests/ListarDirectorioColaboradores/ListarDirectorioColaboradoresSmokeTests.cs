// Issue #590: smoke tests de ListarDirectorioColaboradores, verbo QUERY (RFC 10008, MEF-ADR-0042)
// sobre colaboradores/directorio -- segunda mitad del desglose de #587. No hay proyeccion ni read
// model nuevos: esta clase consulta la MISMA vista materializada DirectorioColaborador via (a')
// session.Query<DirectorioColaborador>(), mismo corte que ListarFichasColaborador (#373) hizo sobre
// la ficha de #356.
//
// Arrange via API, nunca sembrando el event store por fuera de ella: cada colaborador se crea con
// POST Colaboradores (#330) y se termina con POST colaboradores/{id}/vinculaciones/{codigo}:terminar
// (#349/#379) -- los mismos comandos que la proyeccion consume.
//
// Lifecycle Async (MEF-ADR-0034 seccion 3): el worker materializa/actualiza DirectorioColaborador
// DESPUES de que Colaboradores persiste sus eventos. Los casos de exito envuelven la consulta en
// Polling.WaitUntilAsync (timeout estandar 30s) -- unica excepcion documentada al "no usar Polling
// directo en tests".
//
// Aislamiento SIN cleanup en un entorno que ACUMULA entradas de corridas anteriores: un token de
// busqueda generico ("juan") o un numero de documento corto podrian coincidir con historial ajeno.
// Cada test que busca por nombre embebe un Guid en el APELLIDO mismo (sin separador, ej.
// "Bermudez{guidHex}"), formando un token unico en TODAS las corridas pasadas y futuras -- el AND de
// tokens exige que ese token unico este presente, asi que ningun ruido historico puede colarse. La
// busqueda por numero de documento usa un numero fresco (Guid hex) por el mismo motivo.
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.ListarDirectorioColaboradores;

public class ListarDirectorioColaboradoresSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaConsulta = "/api/colaboradores/directorio";
    private const string RutaRegistrar = "/api/Colaboradores";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly HttpMethod MetodoQuery = new("QUERY");

    // Case-insensitive: la respuesta viaja en camelCase (ComposicionServicios configura
    // JsonNamingPolicy.CamelCase), mientras que la forma local de este archivo es PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Forma local DESACOPLADA de la vista de produccion (ReadModels.Colaboradores) y del DTO de
    // respuesta del Function App: replica solo el shape JSON de la respuesta HTTP de ESTE endpoint.
    // Sin TokensNombre -- el endpoint no debe exponerlo (CA-4).
    private sealed record DirectorioColaboradorRespuestaSmoke(
        string Identificacion,
        string TipoDocumento,
        string NumeroDocumento,
        string NombreCompleto,
        string CodigoColaborador,
        string? CodigoSede,
        DateOnly VigenteDesde,
        DateOnly? VigenteHasta);

    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Mismo formato que ColaboradorAggregateRoot.ComputarStreamId (separador "-"), reconstruido
    // localmente (oraculo independiente, MEF-ADR-0002): el smoke test no referencia el Function App.
    private static string ComputarStreamId(string tipo, string numero) => $"{tipo}-{numero}";

    // Apellido con un Guid embebido SIN separador -- tras tokenizar (quitar diacriticos, minusculas,
    // partir por todo caracter no alfanumerico) queda como UN solo token unico en cualquier corrida,
    // pasada o futura, de cualquier archivo de este dominio.
    private static string NuevoApellidoUnico(string prefijo) => $"{prefijo}{Guid.CreateVersion7():N}";

    private static object Filtro(
        IReadOnlyList<string>? identificaciones = null, string? nombre = null,
        object? cursor = null, int take = 50) => new { identificaciones, nombre, cursor, take };

    // Arrange comun: registra un colaborador con una vinculacion abierta -- via el comando que la
    // origina (#330), nunca sembrando el event store por fuera del API.
    private async Task RegistrarColaboradorAsync(
        string tipoIdentificacion, string numeroIdentificacion, DateOnly fechaInicio,
        string primerNombre, string? segundoNombre, string primerApellido, string? segundoApellido,
        string codigoColaborador, CancellationToken ct, string? codigoSede = null)
    {
        var response = await _client.PostAsJsonAsync(RutaRegistrar, new
        {
            tipoIdentificacion,
            numeroIdentificacion,
            primerNombre,
            segundoNombre,
            primerApellido,
            segundoApellido,
            codigoColaborador,
            fechaInicio,
            codigoSede
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que RegistrarColaborador funcione");
    }

    // Arrange comun: cierra la vinculacion vigente -- via el comando que la origina (#349/#379).
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

    private Task<HttpResponseMessage> ConsultarAsync(object filtro, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(MetodoQuery, RutaConsulta)
        {
            Content = JsonContent.Create(filtro)
        };
        return _client.SendAsync(request, ct);
    }

    private async Task<List<DirectorioColaboradorRespuestaSmoke>> ConsultarListaAsync(
        object filtro, CancellationToken ct)
    {
        var response = await ConsultarAsync(filtro, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var lista = await response.Content.ReadFromJsonAsync<List<DirectorioColaboradorRespuestaSmoke>>(
            JsonOptions, cancellationToken: ct);
        return lista ?? [];
    }

    // Reintenta la consulta hasta que la proyeccion asincrona satisfaga la condicion -- unica
    // excepcion documentada al "no usar Polling directo en tests" (lifecycle Async, MEF-ADR-0034
    // seccion 3). Si el timeout se agota es un fallo real (worker no desplegado, o el efecto del
    // arrange nunca se materializo), Polling.WaitUntilAsync lanza TimeoutException.
    private Task<List<DirectorioColaboradorRespuestaSmoke>> ConsultarHastaQueAsync(
        object filtro, Func<List<DirectorioColaboradorRespuestaSmoke>, bool> condicion, CancellationToken ct) =>
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

    // CA-6 (gate): dos colaboradores comparten el MISMO numero de documento con tipos distintos
    // (CC/CE) -- la colision que CA-2 exige resolver por numero suelto. Cada uno lleva un token de
    // apellido unico para que la busqueda por nombre los distinga sin ambiguedad. Se termina la
    // vinculacion de uno para ejercer CA-4 (vigenteHasta no nulo en la terminada, nulo en la
    // abierta) sobre el MISMO dataset.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_EncuentraPorNumeroSinTipoPorNombreYPorIdentificacionesCompletas_CuandoDosColaboradoresComparteNumeroConTiposDistintos()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroCompartido = NuevoNumeroIdentificacion();
        // Apellido CON acentos (issue #590 CA-6): ejercita que la tokenizacion quita diacriticos por
        // igual al escribir (proyeccion) y al buscar (endpoint).
        var apellidoUno = NuevoApellidoUnico("Bérmúdez590");
        var apellidoDos = NuevoApellidoUnico("Rodríguez590");
        var idUno = ComputarStreamId("CC", numeroCompartido);
        var idDos = ComputarStreamId("CE", numeroCompartido);
        var codigoUno = NuevoCodigoColaborador();
        var codigoDos = NuevoCodigoColaborador();
        var fechaInicio = new DateOnly(2026, 1, 1);
        var fechaEfectiva = new DateOnly(2026, 6, 30);

        await RegistrarColaboradorAsync(
            "CC", numeroCompartido, fechaInicio, "[TEST]", "Juan", apellidoUno, null, codigoUno, ct);
        await RegistrarColaboradorAsync(
            "CE", numeroCompartido, fechaInicio, "[TEST]", "Juana", apellidoDos, null, codigoDos, ct);
        await TerminarVinculacionAsync(idDos, codigoDos, fechaEfectiva, ct);

        // Punto de sincronizacion: espera a que la proyeccion asiente AMBOS registros y la
        // terminacion de idDos, buscando por el numero compartido SIN tipo (CA-2).
        var porNumero = await ConsultarHastaQueAsync(
            Filtro(identificaciones: [numeroCompartido]),
            lista => lista.Any(f => f.Identificacion == idUno)
                && lista.Any(f => f.Identificacion == idDos && f.VigenteHasta == fechaEfectiva),
            ct);

        porNumero.Should().Contain(f => f.Identificacion == idUno && f.TipoDocumento == "CC",
            "buscar por el numero sin tipo deberia encontrar a la entrada CC");
        porNumero.Should().Contain(f => f.Identificacion == idDos && f.TipoDocumento == "CE",
            "buscar por el numero sin tipo deberia encontrar TAMBIEN a la entrada CE (colision resuelta por CA-2)");

        // Deterministico desde aqui: la proyeccion ya confirmo estar al dia en el assert anterior.

        // CA-3: nombre parcial sin acentos, con formas distintas a las registradas (mayusculas,
        // sin tilde) -- normalizacion simetrica. Solo el token de apellido de UNO esta presente.
        var busquedaSinAcento = apellidoUno.Replace("Bérmúdez590", "BERMUDEZ590");
        var porNombre = await ConsultarListaAsync(
            Filtro(nombre: $"juan {busquedaSinAcento}"), ct);

        porNombre.Should().ContainSingle(f => f.Identificacion == idUno,
            "'juan {apellido sin acento}' deberia encontrar exactamente a la entrada UNO");
        porNombre.Should().NotContain(f => f.Identificacion == idDos,
            "el token de apellido de UNO no aparece en el nombre de DOS");

        // CA-3: token completo, no prefijo -- "juana" NO matchea el token "juan" de UNO, aunque el
        // apellido buscado (unico) SI pertenece a UNO. Prueba deterministica: el apellido es unico
        // en toda corrida, asi que solo UNO podria matchear, y "juana" se lo impide.
        var conTokenIncompleto = await ConsultarListaAsync(
            Filtro(nombre: $"juana {apellidoUno}"), ct);

        conTokenIncompleto.Should().NotContain(f => f.Identificacion == idUno,
            "'juana' es un token completo distinto de 'juan' -- no deberia matchear por prefijo (CA-3)");

        // CA-3: identificaciones + nombre combinan en AND -- identificacion correcta (idUno) con un
        // nombre que no calza (el token de apellido de DOS) devuelve vacio.
        var conAndSinCoincidencia = await ConsultarListaAsync(
            Filtro(identificaciones: [idUno], nombre: apellidoDos), ct);

        conAndSinCoincidencia.Should().BeEmpty(
            "el AND entre identificaciones y nombre no deberia encontrar nada si el nombre no calza con esa identificacion");

        // CA-2/CA-4: lista de identificaciones completas trae a AMBOS -- la terminada con
        // VigenteHasta no nulo, la abierta con VigenteHasta nulo (el centinela jamas sale por la
        // API). El directorio NO filtra por vigencia: la terminada sigue apareciendo.
        var porIdentificacionesCompletas = await ConsultarListaAsync(
            Filtro(identificaciones: [idUno, idDos]), ct);

        var entradaUno = porIdentificacionesCompletas.Should().ContainSingle(f => f.Identificacion == idUno).Subject;
        entradaUno.VigenteHasta.Should().BeNull(
            "la vinculacion de UNO sigue abierta -- el centinela jamas sale por la API");
        entradaUno.CodigoSede.Should().BeNull("no se asigno sede en el arrange");

        var entradaDos = porIdentificacionesCompletas.Should().ContainSingle(f => f.Identificacion == idDos).Subject;
        entradaDos.VigenteHasta.Should().Be(fechaEfectiva,
            "la vinculacion de DOS fue terminada -- el directorio no filtra por vigencia y sigue apareciendo");

        // CA-4: la respuesta no expone TokensNombre -- estructura interna de busqueda.
        var respuestaCruda = await ConsultarAsync(Filtro(identificaciones: [idUno, idDos]), ct);
        var json = await respuestaCruda.Content.ReadAsStringAsync(ct);
        json.Should().NotContainEquivalentOf("tokensNombre",
            "TokensNombre es estructura interna de indexacion, nunca contrato de respuesta (CA-4)");
    }

    // CA-2: un valor SIN "-" o con tipo desconocido cae al camino de numero de documento, limpiando
    // caracteres no alfanumericos -- " ab-123.456 " y "ab123456" deberian encontrar el mismo PA.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_EncuentraPorNumeroLimpiado_CuandoElValorNoEsUnaIdentificacionCompleta()
    {
        var ct = TestContext.Current.CancellationToken;
        var numero = "AB" + Guid.CreateVersion7().ToString("N")[..8].ToUpperInvariant();
        var id = ComputarStreamId("PA", numero);
        var apellido = NuevoApellidoUnico("Pasaporte590");

        await RegistrarColaboradorAsync(
            "PA", numero, new DateOnly(2026, 1, 1), "[TEST]", null, apellido, null,
            NuevoCodigoColaborador(), ct);

        var conMinusculasSinGuion = numero.ToLowerInvariant();
        var pagina = await ConsultarHastaQueAsync(
            Filtro(identificaciones: [conMinusculasSinGuion]),
            lista => lista.Any(f => f.Identificacion == id),
            ct);

        pagina.Should().ContainSingle(f => f.Identificacion == id,
            "un numero sin guion, en minusculas, deberia normalizar al mismo NumeroDocumento");

        var conEspaciosYPuntuacion = $" {numero[..2]}-{numero[2..5]}.{numero[5..]} ".ToLowerInvariant();
        var paginaConRuido = await ConsultarListaAsync(
            Filtro(identificaciones: [conEspaciosYPuntuacion]), ct);

        paginaConRuido.Should().ContainSingle(f => f.Identificacion == id,
            "espacios y puntuacion se limpian antes de comparar el numero de documento (CA-2)");
    }

    // CA-2: un valor que no corresponde a nadie no produce error -- solo no aporta filas.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_Retorna200ConListaVacia_CuandoNingunaIdentificacionExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(
            Filtro(identificaciones: [$"CC-{Guid.CreateVersion7():N}"]), ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lista = await response.Content.ReadFromJsonAsync<List<DirectorioColaboradorRespuestaSmoke>>(
            JsonOptions, cancellationToken: ct);
        (lista ?? []).Should().BeEmpty();
    }

    // CA-1: Take: 0 se clampea a 1 en el servidor -- sin el clamp, un Take(0) crudo de LINQ
    // devolveria una lista vacia SIEMPRE, sin importar que el colaborador exista.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_ClampeaTakeAMinimoUno_CuandoTakeLlegaEnCero()
    {
        var ct = TestContext.Current.CancellationToken;
        var numero = NuevoNumeroIdentificacion();
        var apellido = NuevoApellidoUnico("Clamp590");
        var id = ComputarStreamId("CC", numero);

        await RegistrarColaboradorAsync(
            "CC", numero, new DateOnly(2026, 1, 1), "[TEST]", null, apellido, null,
            NuevoCodigoColaborador(), ct);

        var pagina = await ConsultarHastaQueAsync(
            Filtro(nombre: apellido, take: 0),
            lista => lista.Any(f => f.Identificacion == id),
            ct);

        pagina.Should().ContainSingle(f => f.Identificacion == id,
            "Take: 0 deberia clampearse a 1 en el servidor -- un Take(0) crudo nunca habria devuelto esta fila");
    }

    // CA-3: paginacion keyset -- el cursor de la ultima fila continua la pagina siguiente sin
    // saltar ni repetir. Dos colaboradores con nombres unicos ordenables (mismo prefijo, sufijo
    // A/B) para forzar un orden predecible entre ellos.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_PaginaSinSaltarNiRepetir_CuandoSeUsaElCursorDeLaUltimaFila()
    {
        var ct = TestContext.Current.CancellationToken;
        var apellidoBase = NuevoApellidoUnico("Paginacion590");
        var apellidoA = $"{apellidoBase}-A";
        var apellidoB = $"{apellidoBase}-B";
        var nombreCompletoA = $"[TEST] {apellidoA}";

        var numeroA = NuevoNumeroIdentificacion();
        var numeroB = NuevoNumeroIdentificacion();
        var idA = ComputarStreamId("CC", numeroA);
        var idB = ComputarStreamId("CC", numeroB);

        await RegistrarColaboradorAsync(
            "CC", numeroA, new DateOnly(2026, 1, 1), "[TEST]", null, apellidoA, null,
            NuevoCodigoColaborador(), ct);
        await RegistrarColaboradorAsync(
            "CC", numeroB, new DateOnly(2026, 1, 1), "[TEST]", null, apellidoB, null,
            NuevoCodigoColaborador(), ct);

        // Pagina 1: cursor posicionado exactamente en el nombre de A (Identificacion vacia ordena
        // antes que cualquier Id real) -- con Take: 1 trae solo a A.
        var pagina1 = await ConsultarHastaQueAsync(
            Filtro(cursor: new { nombreCompleto = nombreCompletoA, identificacion = "" }, take: 1),
            lista => lista.Any(f => f.Identificacion == idA),
            ct);

        pagina1.Should().ContainSingle(f => f.Identificacion == idA,
            "el cursor posicionado en el nombre de A, con Identificacion vacia, deberia traer solo a A");

        // Pagina 2: cursor REAL de la ultima fila de la pagina 1 -- debe continuar exactamente en
        // B, sin saltarlo ni repetir A.
        var pagina2 = await ConsultarHastaQueAsync(
            Filtro(cursor: new { nombreCompleto = nombreCompletoA, identificacion = idA }, take: 1),
            lista => lista.Any(f => f.Identificacion == idB),
            ct);

        pagina2.Should().ContainSingle(f => f.Identificacion == idB,
            "el cursor (NombreCompleto, Identificacion) de A deberia continuar exactamente en B, sin saltar ni repetir");
    }

    // CA-1: sin Content-Type: application/json -> 415, verificado ANTES de leer el body.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_Retorna415_CuandoContentTypeNoEsJson()
    {
        var ct = TestContext.Current.CancellationToken;

        using var request = new HttpRequestMessage(MetodoQuery, RutaConsulta)
        {
            Content = new StringContent("{}", Encoding.UTF8, "text/plain")
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    // CA-1: body con Content-Type json pero sintacticamente invalido -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_Retorna400_CuandoElBodyEsJsonInvalido()
    {
        var ct = TestContext.Current.CancellationToken;

        using var request = new HttpRequestMessage(MetodoQuery, RutaConsulta)
        {
            Content = new StringContent("{ esto no es json valido", Encoding.UTF8, "application/json")
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-1: body ausente (Content-Type json, cero bytes) -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_Retorna400_CuandoElBodyEstaVacio()
    {
        var ct = TestContext.Current.CancellationToken;

        using var request = new HttpRequestMessage(MetodoQuery, RutaConsulta)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-1: JSON valido pero sin Identificaciones ni Nombre -> 422 (al menos uno es obligatorio).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoFaltanIdentificacionesYNombre()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(new { }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // CA-1: Identificaciones presente pero vacia -> 422.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoIdentificacionesVieneVacia()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(new { identificaciones = Array.Empty<string>() }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // CA-1: un valor en blanco dentro de Identificaciones -> 422.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoIdentificacionesTraeUnValorEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(new { identificaciones = new[] { "   " } }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // CA-1: mas de 200 valores en Identificaciones -> 422.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoIdentificacionesSuperaDoscientosValores()
    {
        var ct = TestContext.Current.CancellationToken;
        var identificaciones = Enumerable.Range(0, 201).Select(i => $"CC-{i}").ToArray();

        var response = await ConsultarAsync(new { identificaciones }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // CA-1: Nombre presente pero en blanco -> 422 (omitir el campo, no enviarlo vacio).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoNombreVieneEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(new { nombre = "   " }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // CA-1: cursor con un solo campo presente (falta Identificacion) -> 422.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoElCursorLlegaIncompleto()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = new
        {
            nombre = "[TEST]",
            cursor = new { nombreCompleto = "[TEST] Algo" } // sin identificacion
        };

        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
