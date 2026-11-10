// colaboradores/directorio se consulta con el verbo QUERY (RFC 10008, MEF-ADR-0042) sobre la vista
// DirectorioColaborador, que el worker materializa de forma asincrona.
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

    // Forma local desacoplada de la vista y del DTO de produccion: solo el shape JSON. Sin TokensNombre,
    // que el endpoint no debe exponer.
    private sealed record DirectorioColaboradorRespuestaSmoke(
        string Identificacion,
        string TipoDocumento,
        string NumeroDocumento,
        string NombreCompleto,
        string CodigoColaborador,
        string? CodigoSede,
        DateOnly VigenteDesde,
        DateOnly? VigenteHasta);

    private sealed record ListaSmoke(List<DirectorioColaboradorRespuestaSmoke> Elementos, string? SiguienteCursor);

    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Mismo formato que ColaboradorAggregateRoot.ComputarStreamId (separador "-"), reconstruido
    // localmente (oraculo independiente, MEF-ADR-0002): el smoke test no referencia el Function App.
    private static string ComputarStreamId(string tipo, string numero) => $"{tipo}-{numero}";

    // Sin cleanup el entorno acumula historial y un token generico ("juan") podria coincidir. El Guid va
    // SIN separador para que, tras tokenizar (sin diacriticos, minusculas, partido por no alfanumericos),
    // quede como UN solo token unico en cualquier corrida.
    private static string NuevoApellidoUnico(string prefijo) => $"{prefijo}{Guid.CreateVersion7():N}";

    private static object Filtro(
        IReadOnlyList<string>? identificaciones = null, string? nombre = null,
        string? cursor = null, int? take = null) => new { identificaciones, nombre, cursor, take };

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

    private Task<HttpResponseMessage> ConsultarAsync(object filtro, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(MetodoQuery, RutaConsulta)
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

    private async Task<List<DirectorioColaboradorRespuestaSmoke>> ConsultarListaAsync(
        object filtro, CancellationToken ct) => (await ConsultarSobreAsync(filtro, ct)).Elementos;

    // La proyeccion es Async (MEF-ADR-0034 seccion 3): un timeout agotado es un fallo real (worker no
    // desplegado, o el arrange nunca se materializo).
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

    // Mismo numero con tipos distintos (CC/CE): la colision que la busqueda por numero suelto debe
    // resolver. Se termina uno para comparar VigenteHasta sobre el mismo dataset.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_EncuentraPorNumeroSinTipoPorNombreYPorIdentificacionesCompletas_CuandoDosColaboradoresComparteNumeroConTiposDistintos()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroCompartido = NuevoNumeroIdentificacion();
        // Con acentos: la tokenizacion debe quitar diacriticos por igual al escribir (proyeccion) y al
        // buscar (endpoint).
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

        // Punto de sincronizacion: espera a que la proyeccion asiente ambos registros y la terminacion.
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

        // Formas distintas a las registradas (mayusculas, sin tilde): normalizacion simetrica.
        var busquedaSinAcento = apellidoUno.Replace("Bérmúdez590", "BERMUDEZ590");
        var porNombre = await ConsultarListaAsync(
            Filtro(nombre: $"juan {busquedaSinAcento}"), ct);

        porNombre.Should().ContainSingle(f => f.Identificacion == idUno,
            "buscar 'juan' mas el apellido sin acento deberia encontrar exactamente a la entrada UNO");
        porNombre.Should().NotContain(f => f.Identificacion == idDos,
            "el token de apellido de UNO no aparece en el nombre de DOS");

        // Token completo, no prefijo: el apellido unico solo puede ser de UNO y "juana" se lo impide.
        var conTokenIncompleto = await ConsultarListaAsync(
            Filtro(nombre: $"juana {apellidoUno}"), ct);

        conTokenIncompleto.Should().NotContain(f => f.Identificacion == idUno,
            "'juana' es un token completo distinto de 'juan' -- no deberia matchear por prefijo (CA-3)");

        var conAndSinCoincidencia = await ConsultarListaAsync(
            Filtro(identificaciones: [idUno], nombre: apellidoDos), ct);

        conAndSinCoincidencia.Should().BeEmpty(
            "el AND entre identificaciones y nombre no deberia encontrar nada si el nombre no calza con esa identificacion");

        var porIdentificacionesCompletas = await ConsultarListaAsync(
            Filtro(identificaciones: [idUno, idDos]), ct);

        var entradaUno = porIdentificacionesCompletas.Should().ContainSingle(f => f.Identificacion == idUno).Subject;
        entradaUno.VigenteHasta.Should().BeNull(
            "la vinculacion de UNO sigue abierta -- el centinela jamas sale por la API");
        entradaUno.CodigoSede.Should().BeNull("no se asigno sede en el arrange");

        var entradaDos = porIdentificacionesCompletas.Should().ContainSingle(f => f.Identificacion == idDos).Subject;
        entradaDos.VigenteHasta.Should().Be(fechaEfectiva,
            "la vinculacion de DOS fue terminada -- el directorio no filtra por vigencia y sigue apareciendo");

        var respuestaCruda = await ConsultarAsync(Filtro(identificaciones: [idUno, idDos]), ct);
        var json = await respuestaCruda.Content.ReadAsStringAsync(ct);
        json.Should().NotContainEquivalentOf("tokensNombre",
            "TokensNombre es estructura interna de indexacion, nunca contrato de respuesta (CA-4)");
    }

    // Un valor SIN "-" o con tipo desconocido cae al camino de numero de documento, que limpia los
    // caracteres no alfanumericos.
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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_Retorna200ConListaVacia_CuandoNingunaIdentificacionExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(
            Filtro(identificaciones: [$"CC-{Guid.CreateVersion7():N}"]), ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lista = await response.Content.ReadFromJsonAsync<ListaSmoke>(JsonOptions, cancellationToken: ct);
        lista!.Elementos.Should().BeEmpty();
        lista.SiguienteCursor.Should().BeNull();
    }

    // El token unico de apellido acota el filtro a A y B (el sufijo "-A"/"-B" tokeniza aparte), asi que
    // con take 1 la pagina 1 es A con cursor y la pagina 2 es B sin cursor.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_PaginaSinSaltarNiRepetir_CuandoSeUsaElSiguienteCursor()
    {
        var ct = TestContext.Current.CancellationToken;
        var apellidoBase = NuevoApellidoUnico("Paginacion590");

        var numeroA = NuevoNumeroIdentificacion();
        var numeroB = NuevoNumeroIdentificacion();
        var idA = ComputarStreamId("CC", numeroA);
        var idB = ComputarStreamId("CC", numeroB);

        await RegistrarColaboradorAsync(
            "CC", numeroA, new DateOnly(2026, 1, 1), "[TEST]", null, $"{apellidoBase}-A", null,
            NuevoCodigoColaborador(), ct);
        await RegistrarColaboradorAsync(
            "CC", numeroB, new DateOnly(2026, 1, 1), "[TEST]", null, $"{apellidoBase}-B", null,
            NuevoCodigoColaborador(), ct);

        // Sin take: punto de sincronizacion -- ambos materializados y todo llega sin cursor.
        var todos = await Polling.WaitUntilAsync(async () =>
        {
            var lista = await ConsultarSobreAsync(Filtro(nombre: apellidoBase), ct);
            return lista.Elementos.Count == 2 ? lista : null;
        }, Timeout);
        todos.SiguienteCursor.Should().BeNull("sin take el directorio devuelve todas las coincidencias");

        var pagina1 = await ConsultarSobreAsync(Filtro(nombre: apellidoBase, take: 1), ct);
        pagina1.Elementos.Should().ContainSingle(f => f.Identificacion == idA);
        pagina1.SiguienteCursor.Should().NotBeNullOrEmpty("queda B por recorrer");

        var pagina2 = await ConsultarSobreAsync(
            Filtro(nombre: apellidoBase, cursor: pagina1.SiguienteCursor, take: 1), ct);
        pagina2.Elementos.Should().ContainSingle(f => f.Identificacion == idB,
            "el siguienteCursor de A deberia continuar exactamente en B, sin saltar ni repetir");
        pagina2.SiguienteCursor.Should().BeNull("B es la ultima coincidencia");
    }

    // El 415 se decide ANTES de leer el body.
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

    // Al menos uno de los dos es obligatorio.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoFaltanIdentificacionesYNombre()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(new { }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoIdentificacionesVieneVacia()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(new { identificaciones = Array.Empty<string>() }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoIdentificacionesTraeUnValorEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(new { identificaciones = new[] { "   " } }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // Si no se filtra por nombre, el campo se omite; no se envia vacio.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoNombreVieneEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(new { nombre = "   " }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // Un cursor que no salio del servidor -> 422; el siguiente test cubre el objeto de la forma vieja.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoElCursorNoEsValido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(new { nombre = "[TEST]", cursor = "no-es-un-cursor" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDirectorioColaboradores_Retorna422_CuandoElCursorTraeLaFormaVieja()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = new
        {
            nombre = "[TEST]",
            cursor = new { nombreCompleto = "[TEST] Algo", identificacion = "CC-1" }
        };

        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
