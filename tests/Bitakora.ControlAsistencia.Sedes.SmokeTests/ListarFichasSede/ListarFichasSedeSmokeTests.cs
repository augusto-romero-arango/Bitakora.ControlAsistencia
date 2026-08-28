// Issue #461: smoke tests de ListarFichasSede, GET sedes/fichas -- mismo recurso que
// ObtenerFichaSede, sin QUERY: el filtro Activa es un unico par campo=valor en igualdad
// (MEF-ADR-0042 seccion 1) y SIN paginacion (decision de sesion 2026-08-27, Rule of Three,
// MEF-ADR-0018).
//
// Arrange via API, nunca sembrando el event store por fuera de ella: cada sede se crea con POST
// sedes (#456) y se desactiva con POST sedes/{codigo}:desactivar (#459) -- los mismos comandos que
// la proyeccion consume.
//
// Lifecycle Async (MEF-ADR-0034 seccion 3): el worker materializa/actualiza FichaSede DESPUES de
// que Sedes persiste sus eventos. Los casos de exito envuelven la consulta en Polling.WaitUntilAsync
// (timeout estandar 30s) -- unica excepcion documentada al "no usar Polling directo en tests".
//
// Aislamiento de datos SIN cleanup, en un entorno SIN paginacion que ACUMULA fichas de corridas
// anteriores: cada test filtra su propia fila por Codigo unico (Guid), nunca por conteo total ni
// posicion/indice.
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

    // Forma local DESACOPLADA del read model de produccion (ReadModels.Sedes.FichaSede): replica
    // solo el shape JSON de la respuesta HTTP de este endpoint. El smoke test no referencia
    // ReadModels ni el Function App (isla, MEF-ADR-0034 seccion 5).
    private sealed record FichaSedeRespuestaSmoke(
        string Id,
        string Codigo,
        string Nombre,
        string? Ciudad,
        string? Direccion,
        string? CentroDeCostos,
        bool Activa,
        IReadOnlyList<string> Dispositivos);

    // Prefijo "TEST-" y no "[TEST] ": el Codigo viaja en la ruta/query y esta sujeto al charset
    // URL-safe, del que "[", "]" y el espacio quedan fuera.
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
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que RegistrarSede funcione");
    }

    private async Task DesactivarSedeAsync(string codigo, CancellationToken ct)
    {
        var response = await _client.PostAsync(RutaDesactivar(codigo), null, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que DesactivarSede funcione");
    }

    // Reintenta la ficha puntual hasta que la proyeccion asincrona la materialice y cumpla la
    // condicion pedida -- garantiza, de forma deterministica, que el listado que se pruebe despues
    // ya esta al dia (mismo criterio que ObtenerFichaColaboradorSmokeTests).
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

        var lista = await response.Content.ReadFromJsonAsync<List<FichaSedeRespuestaSmoke>>(
            JsonOptions, cancellationToken: ct);
        return lista ?? [];
    }

    // Reintenta el listado hasta que la proyeccion asincrona satisfaga la condicion -- unica
    // excepcion documentada al "no usar Polling directo en tests" (lifecycle Async, MEF-ADR-0034
    // seccion 3).
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

    // Listado (sin filtro): la sede recien registrada aparece en la coleccion completa, filtrando
    // por su Codigo unico -- nunca por posicion/indice, en un entorno que acumula fichas de
    // corridas anteriores.
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

    // CA-6: filtrar por activa=true devuelve la sede activa y excluye la inactiva; activa=false es
    // lo contrario. Se espera primero puntualmente la sede desactivada (confirma que la proyeccion
    // YA aplico SedeDesactivada) antes de probar el filtro de forma deterministica -- de lo
    // contrario un filtro fallido podria confundirse con una proyeccion desactualizada.
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

    // Filtro sintacticamente invalido ("activa" no parsea a bool) -> 400, verificado explicitamente
    // por el endpoint antes de tocar Marten.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasSede_Retorna400_CuandoElFiltroActivaNoEsBooleano()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync($"{RutaListado}?activa=no-es-booleano", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
