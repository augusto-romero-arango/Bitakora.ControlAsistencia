// Arrange via API, nunca sembrando el event store por fuera de ella: los mismos comandos que la
// proyeccion consume.
//
// FichaSede se materializa de forma asincrona (MEF-ADR-0034 seccion 3): los casos de exito envuelven
// la consulta en Polling.WaitUntilAsync, unica excepcion documentada al "no usar Polling directo en
// tests".
//
// Sin cleanup y en un entorno que acumula fichas de corridas anteriores: cada test filtra su propia
// fila por Codigo unico, nunca por conteo total ni posicion.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Sedes.SmokeTests.ListarFichasSede;

public class ListarFichasSedeSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaListado = "/api/sedes/fichas";
    private const string RutaRegistrar = "/api/sedes";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Case-insensitive: la respuesta viaja en camelCase (ComposicionServicios configura
    // JsonNamingPolicy.CamelCase), mientras que las formas locales de este archivo son PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Forma local desacoplada de ReadModels.Sedes.FichaSede: el smoke test no referencia ReadModels
    // ni el Function App (MEF-ADR-0034 seccion 5).
    private sealed record FichaSedeRespuestaSmoke(
        string Id,
        string Codigo,
        string Nombre,
        string? Ciudad,
        string? Direccion,
        string? CentroDeCostos,
        bool Activa,
        IReadOnlyList<string> Dispositivos);

    private sealed record ListaFichasSedeRespuestaSmoke(
        List<FichaSedeRespuestaSmoke> Elementos,
        string? SiguienteCursor);

    // "TEST-" y no "[TEST] ": el Codigo viaja en la ruta/query y "[", "]" y el espacio no son URL-safe.
    private static string NuevoCodigo() => $"TEST-{Guid.CreateVersion7()}";

    private static string RutaFicha(string codigo) => $"/api/sedes/fichas/{codigo}";

    private static string RutaDesactivar(string codigo) => $"/api/sedes/{codigo}:desactivar";

    private static object PayloadRegistro(string codigo) => new
    {
        codigo,
        nombre = "[TEST] Sede Smoke",
        ciudad = (string?)null,
        direccion = (string?)null
    };

    private async Task RegistrarSedeAsync(string codigo, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(RutaRegistrar, PayloadRegistro(codigo), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que RegistrarSede funcione");
    }

    private async Task DesactivarSedeAsync(string codigo, CancellationToken ct)
    {
        var response = await _client.PostAsync(RutaDesactivar(codigo), null, ct);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que DesactivarSede funcione");
    }

    // Reintenta la ficha puntual hasta que la proyeccion la materialice y cumpla la condicion: asi el
    // listado que se pruebe despues ya esta al dia.
    private Task<FichaSedeRespuestaSmoke> EsperarFichaAsync(
        string codigo, CancellationToken ct, Func<FichaSedeRespuestaSmoke, bool> hasta) =>
        Polling.WaitUntilAsync(async () =>
        {
            var response = await _client.GetAsync(RutaFicha(codigo), ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<FichaSedeRespuestaSmoke>(
                JsonOptions, cancellationToken: ct);

            return body is not null && hasta(body) ? body : null;
        }, Timeout);

    private async Task<List<FichaSedeRespuestaSmoke>> ListarAsync(CancellationToken ct, bool? activa = null)
    {
        var ruta = activa is null
            ? RutaListado
            : $"{RutaListado}?activa={(activa.Value ? "true" : "false")}";

        var response = await _client.GetAsync(ruta, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var sobre = await response.Content.ReadFromJsonAsync<ListaFichasSedeRespuestaSmoke>(
            JsonOptions, cancellationToken: ct);
        return sobre?.Elementos ?? [];
    }

    private async Task<ListaFichasSedeRespuestaSmoke> ListarPaginaAsync(string query, CancellationToken ct)
    {
        var response = await _client.GetAsync($"{RutaListado}?{query}", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var sobre = await response.Content.ReadFromJsonAsync<ListaFichasSedeRespuestaSmoke>(
            JsonOptions, cancellationToken: ct);
        return sobre!;
    }

    private Task<List<FichaSedeRespuestaSmoke>> ListarHastaQueAsync(
        Func<List<FichaSedeRespuestaSmoke>, bool> condicion, CancellationToken ct, bool? activa = null) =>
        Polling.WaitUntilAsync(async () =>
        {
            var lista = await ListarAsync(ct, activa);
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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasSede_IncluyeLaFichaCreada_CuandoNoSeAplicaFiltro()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigo = NuevoCodigo();

        await RegistrarSedeAsync(codigo, ct);

        var lista = await ListarHastaQueAsync(l => l.Any(f => f.Codigo == codigo), ct);

        var ficha = lista.Should().ContainSingle(f => f.Codigo == codigo).Subject;
        ficha.Activa.Should().BeTrue("la sede nace activa (CA-1)");
        ficha.Dispositivos.Should().BeEmpty();
    }

    // Se espera primero la ficha puntual desactivada: confirma que la proyeccion ya aplico
    // SedeDesactivada, y un filtro fallido no se confunde con una proyeccion desactualizada.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasSede_FiltraPorActiva_CuandoElParametroLlegaEnLaQueryString()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoActiva = NuevoCodigo();
        var codigoInactiva = NuevoCodigo();

        await RegistrarSedeAsync(codigoActiva, ct);
        await RegistrarSedeAsync(codigoInactiva, ct);
        await DesactivarSedeAsync(codigoInactiva, ct);

        await EsperarFichaAsync(codigoInactiva, ct, f => !f.Activa);
        await EsperarFichaAsync(codigoActiva, ct, f => f.Activa);

        var listaActivas = await ListarAsync(ct, activa: true);
        listaActivas.Should().Contain(f => f.Codigo == codigoActiva);
        listaActivas.Should().NotContain(f => f.Codigo == codigoInactiva);

        var listaInactivas = await ListarAsync(ct, activa: false);
        listaInactivas.Should().Contain(f => f.Codigo == codigoInactiva);
        listaInactivas.Should().NotContain(f => f.Codigo == codigoActiva);
    }

    // "activa" que no parsea a bool -> 400, verificado por el endpoint antes de tocar Marten.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasSede_Retorna400_CuandoElFiltroActivaNoEsBooleano()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync($"{RutaListado}?activa=no-es-booleano", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-ADR-0039: recorrido de dos paginas con take=1 y siguienteCursor (sin validar tamanos, CA-ADR-0041).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasSede_RecorreDosPaginas_CuandoSeUsaTakeYSiguienteCursor()
    {
        var ct = TestContext.Current.CancellationToken;
        await RegistrarSedeAsync(NuevoCodigo(), ct);
        await RegistrarSedeAsync(NuevoCodigo(), ct);

        var primera = await Polling.WaitUntilAsync(async () =>
        {
            var pagina = await ListarPaginaAsync("take=1", ct);
            return pagina.SiguienteCursor is not null ? pagina : null;
        }, Timeout);

        primera.Elementos.Should().HaveCount(1);

        var segunda = await ListarPaginaAsync(
            $"take=1&cursor={Uri.EscapeDataString(primera.SiguienteCursor!)}", ct);

        segunda.Elementos.Should().HaveCount(1);
        segunda.Elementos[0].Codigo.Should().NotBe(primera.Elementos[0].Codigo);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasSede_Retorna400_CuandoElCursorEsInvalido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync($"{RutaListado}?cursor=%25%25no-es-un-cursor", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
