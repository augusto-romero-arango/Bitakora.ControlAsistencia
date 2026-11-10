using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.ListarCategoriasDeEtiquetas;

public class ListarCategoriasDeEtiquetasSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaCatalogo = "/api/colaboradores/etiquetas/categorias";
    private const string RutaRegistrar = "/api/Colaboradores";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Case-insensitive: la respuesta viaja en camelCase (ComposicionServicios configura
    // JsonNamingPolicy.CamelCase), mientras que las formas locales de este archivo son PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Formas locales desacopladas del read model: el smoke no referencia ReadModels (MEF-ADR-0034
    // seccion 5); solo replican el shape JSON de la respuesta.
    private sealed record ValorCategoriaSmoke(string Valor, string ValorNormalizado);

    private sealed record CategoriaDeEtiquetasSmoke(
        string Id, string Categoria, IReadOnlyList<ValorCategoriaSmoke> Valores);

    private sealed record ListaSmoke(List<CategoriaDeEtiquetasSmoke> Elementos, string? SiguienteCursor);

    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // El catalogo es global y acumulativo (nada sale nunca) y no hay cleanup: cada test aisla su categoria
    // con este sufijo y la busca por Id, nunca por posicion. "N" (hex en minusculas) sobrevive a la
    // normalizacion de Etiqueta. Las formas discriminadoras van sin tildes: la normalizacion Unicode ya la
    // prueban el unit test del VO y AsignarEtiquetaSmokeTests.
    private static string NuevoSufijoUnico() => Guid.CreateVersion7().ToString("N");

    // Oraculo independiente (MEF-ADR-0002), reconstruido a mano; es tambien el {id} de ruta.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    // La categoria puede traer tildes y se escapa.
    private static string RutaEtiqueta(string id, string categoria) =>
        $"/api/colaboradores/{id}/etiquetas/{Uri.EscapeDataString(categoria)}";

    private async Task RegistrarColaboradorAsync(
        string numeroIdentificacion, DateOnly fechaInicio, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(RutaRegistrar, new
        {
            tipoIdentificacion = TipoIdentificacionCc,
            numeroIdentificacion,
            primerNombre = "[TEST]",
            segundoNombre = (string?)null,
            primerApellido = "Smoke",
            segundoApellido = (string?)null,
            codigoColaborador = NuevoCodigoColaborador(),
            fechaInicio
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que RegistrarColaborador funcione");
    }

    private async Task AsignarEtiquetaAsync(
        string numeroIdentificacion, string categoria, string valor, CancellationToken ct)
    {
        var id = ComputarStreamId(numeroIdentificacion);
        var response = await _client.PutAsJsonAsync(RutaEtiqueta(id, categoria), new { valor }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que AsignarEtiqueta funcione");
    }

    private async Task RetirarEtiquetaAsync(
        string numeroIdentificacion, string categoria, CancellationToken ct)
    {
        var id = ComputarStreamId(numeroIdentificacion);
        var response = await _client.DeleteAsync(RutaEtiqueta(id, categoria), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que RetirarEtiqueta funcione");
    }

    private async Task<ListaSmoke> ListarAsync(string query, CancellationToken ct)
    {
        var response = await _client.GetAsync(RutaCatalogo + query, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var lista = await response.Content.ReadFromJsonAsync<ListaSmoke>(JsonOptions, cancellationToken: ct);
        return lista!;
    }

    private async Task<List<CategoriaDeEtiquetasSmoke>> ObtenerCatalogoAsync(CancellationToken ct)
    {
        var lista = await ListarAsync("", ct);
        lista.SiguienteCursor.Should().BeNull("sin take el catalogo llega completo");
        return lista.Elementos;
    }

    // La proyeccion es Async (MEF-ADR-0034 seccion 3). Un timeout agotado es un fallo real (worker no
    // desplegado, proyeccion sin registrar, lifecycle equivocado), nunca un Skip.
    // Para afirmar una AUSENCIA se espera un centinela asignado al final: el daemon aplica en el orden de
    // la secuencia global y los comandos van en serie, asi que si el centinela aparece, todo lo anterior
    // ya se aplico. Es la forma deterministica de probarla, sin Task.Delay.
    private Task<List<CategoriaDeEtiquetasSmoke>> EsperarCatalogoAsync(
        Func<List<CategoriaDeEtiquetasSmoke>, bool> condicion, CancellationToken ct) =>
        Polling.WaitUntilAsync(async () =>
        {
            var catalogo = await ObtenerCatalogoAsync(ct);
            return condicion(catalogo) ? catalogo : null;
        }, Timeout);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Solo afirma el 200 con una coleccion: el catalogo vacio no se puede forzar en dev (compartido y
    // acumulativo) sin borrar datos de otras corridas.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCategoriasDeEtiquetas_Retorna200ConUnaColeccion_Siempre()
    {
        var ct = TestContext.Current.CancellationToken;

        var catalogo = await ObtenerCatalogoAsync(ct);

        catalogo.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCategoriasDeEtiquetas_DevuelveCategoriasDistintasConSusValores_CuandoVariosColaboradoresAsignanEtiquetasDistintas()
    {
        var ct = TestContext.Current.CancellationToken;
        var categoriaA = $"area357-{NuevoSufijoUnico()}";
        var categoriaB = $"cargo357-{NuevoSufijoUnico()}";
        var valorA = "tecnologia";
        var valorB = "desarrollador";

        var numeroA = NuevoNumeroIdentificacion();
        var numeroB = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numeroA, new DateOnly(2026, 1, 1), ct);
        await RegistrarColaboradorAsync(numeroB, new DateOnly(2026, 1, 1), ct);
        await AsignarEtiquetaAsync(numeroA, categoriaA, valorA, ct);
        await AsignarEtiquetaAsync(numeroB, categoriaB, valorB, ct);

        var catalogo = await EsperarCatalogoAsync(
            lista => lista.Any(c => c.Id == categoriaA) && lista.Any(c => c.Id == categoriaB), ct);

        var vistaA = catalogo.Should().ContainSingle(c => c.Id == categoriaA).Subject;
        vistaA.Categoria.Should().Be(categoriaA);
        vistaA.Valores.Should().ContainSingle(v => v.ValorNormalizado == valorA)
            .Which.Valor.Should().Be(valorA);

        var vistaB = catalogo.Should().ContainSingle(c => c.Id == categoriaB).Subject;
        vistaB.Categoria.Should().Be(categoriaB);
        vistaB.Valores.Should().ContainSingle(v => v.ValorNormalizado == valorB)
            .Which.Valor.Should().Be(valorB);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCategoriasDeEtiquetas_NoDuplicaCategoriaNiValor_CuandoSeAsignaElMismoParDosVeces()
    {
        var ct = TestContext.Current.CancellationToken;
        var categoria = $"duplicado357-{NuevoSufijoUnico()}";
        var valor = "mismovalor";
        var categoriaCentinela = $"centinela357-{NuevoSufijoUnico()}";

        var numero1 = NuevoNumeroIdentificacion();
        var numero2 = NuevoNumeroIdentificacion();
        var numeroCentinela = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numero1, new DateOnly(2026, 1, 1), ct);
        await RegistrarColaboradorAsync(numero2, new DateOnly(2026, 1, 1), ct);
        await RegistrarColaboradorAsync(numeroCentinela, new DateOnly(2026, 1, 1), ct);

        await AsignarEtiquetaAsync(numero1, categoria, valor, ct);
        await AsignarEtiquetaAsync(numero2, categoria, valor, ct);
        await AsignarEtiquetaAsync(numeroCentinela, categoriaCentinela, "checkpoint", ct);

        var catalogo = await EsperarCatalogoAsync(lista => lista.Any(c => c.Id == categoriaCentinela), ct);

        var vista = catalogo.Should().ContainSingle(c => c.Id == categoria).Subject;
        vista.Valores.Should().ContainSingle(v => v.ValorNormalizado == valor,
            "el mismo par (categoria, valor) asignado dos veces no deberia duplicar ni la categoria ni el valor");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCategoriasDeEtiquetas_ColapsaEnUnaSolaCategoriaConDisplayDeLaUltima_CuandoDosFormasNormalizanIgual()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = NuevoSufijoUnico();
        var categoriaMayusculas = $"COLAPSO357-{sufijo}";
        var categoriaMinusculas = categoriaMayusculas.ToLowerInvariant(); // misma categoria normalizada
        var categoriaCentinela = $"centinela357-{NuevoSufijoUnico()}";

        var numero1 = NuevoNumeroIdentificacion();
        var numero2 = NuevoNumeroIdentificacion();
        var numeroCentinela = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numero1, new DateOnly(2026, 1, 1), ct);
        await RegistrarColaboradorAsync(numero2, new DateOnly(2026, 1, 1), ct);
        await RegistrarColaboradorAsync(numeroCentinela, new DateOnly(2026, 1, 1), ct);

        await AsignarEtiquetaAsync(numero1, categoriaMayusculas, "PRIMERVALOR", ct);
        // Misma etiqueta por valor (categoria y valor normalizan igual), forma display distinta:
        // "la ultima gana" debe reflejarse tanto en Categoria como en el Valor del ValorCategoria.
        await AsignarEtiquetaAsync(numero2, categoriaMinusculas, "primervalor", ct);
        await AsignarEtiquetaAsync(numeroCentinela, categoriaCentinela, "checkpoint", ct);

        var catalogo = await EsperarCatalogoAsync(lista => lista.Any(c => c.Id == categoriaCentinela), ct);

        var vista = catalogo.Should().ContainSingle(c => c.Id == categoriaMinusculas,
            "dos formas originales que normalizan igual deberian colapsar en UNA sola categoria").Subject;

        vista.Categoria.Should().Be(categoriaMinusculas,
            "el display de la categoria deberia reflejar la ULTIMA asignacion que la toco");
        vista.Valores.Should().ContainSingle(
                "el mismo valor por forma distinta tambien deberia colapsar (upsert por ValorNormalizado)")
            .Which.Valor.Should().Be("primervalor",
                "el display del valor tambien refleja la ultima asignacion");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCategoriasDeEtiquetas_AgregaElValorNuevoYConservaElAnterior_CuandoSeSobrescribeUnaCategoriaExistente()
    {
        var ct = TestContext.Current.CancellationToken;
        var categoria = $"acumulativo357-{NuevoSufijoUnico()}";
        var valorAnterior = "valoranterior";
        var valorNuevo = "valornuevo";

        var numero = NuevoNumeroIdentificacion();
        await RegistrarColaboradorAsync(numero, new DateOnly(2026, 1, 1), ct);

        await AsignarEtiquetaAsync(numero, categoria, valorAnterior, ct);
        await AsignarEtiquetaAsync(numero, categoria, valorNuevo, ct);

        var catalogo = await EsperarCatalogoAsync(
            lista => lista.Any(c => c.Id == categoria
                && c.Valores.Any(v => v.ValorNormalizado == valorAnterior)
                && c.Valores.Any(v => v.ValorNormalizado == valorNuevo)),
            ct);

        var vista = catalogo.Should().ContainSingle(c => c.Id == categoria).Subject;
        vista.Valores.Should().Contain(v => v.ValorNormalizado == valorAnterior,
            "el catalogo es ACUMULATIVO: sobrescribir no deberia borrar el valor anterior");
        vista.Valores.Should().Contain(v => v.ValorNormalizado == valorNuevo,
            "sobrescribir deberia agregar el valor nuevo al catalogo");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCategoriasDeEtiquetas_ConservaLaCategoriaYElValor_CuandoSeRetiraLaEtiqueta()
    {
        var ct = TestContext.Current.CancellationToken;
        var categoria = $"retiro357-{NuevoSufijoUnico()}";
        var valor = "valorretirado";
        var categoriaCentinela = $"centinela357-{NuevoSufijoUnico()}";

        var numero = NuevoNumeroIdentificacion();
        var numeroCentinela = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numero, new DateOnly(2026, 1, 1), ct);
        await RegistrarColaboradorAsync(numeroCentinela, new DateOnly(2026, 1, 1), ct);

        await AsignarEtiquetaAsync(numero, categoria, valor, ct);

        // No encadenar eventos sobre una vista que todavia no reflejo el primero.
        await EsperarCatalogoAsync(lista => lista.Any(c => c.Id == categoria), ct);

        await RetirarEtiquetaAsync(numero, categoria, ct);
        await AsignarEtiquetaAsync(numeroCentinela, categoriaCentinela, "checkpoint", ct);

        var catalogo = await EsperarCatalogoAsync(lista => lista.Any(c => c.Id == categoriaCentinela), ct);

        var vista = catalogo.Should().ContainSingle(c => c.Id == categoria,
            "EtiquetaRetirada no deberia alterar el catalogo (acumulativo, CA-5)").Subject;
        vista.Valores.Should().ContainSingle(v => v.ValorNormalizado == valor);
    }

    // El catalogo es global: se afirma la continuidad del recorrido (sin repetir ni saltar), no su contenido.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCategoriasDeEtiquetas_PaginaConElSiguienteCursorSinRepetirNiSaltar_CuandoSeUsaTake()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var categoriaA = $"pagina1000-a-{NuevoSufijoUnico()}";
        var categoriaB = $"pagina1000-b-{NuevoSufijoUnico()}";

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 1), ct);
        await AsignarEtiquetaAsync(numeroIdentificacion, categoriaA, "uno", ct);
        await AsignarEtiquetaAsync(numeroIdentificacion, categoriaB, "dos", ct);
        await EsperarCatalogoAsync(
            catalogo => catalogo.Any(c => c.Id == categoriaA) && catalogo.Any(c => c.Id == categoriaB), ct);

        var referencia = await ListarAsync("?take=2", ct);
        var pagina1 = await ListarAsync("?take=1", ct);
        pagina1.SiguienteCursor.Should().NotBeNullOrEmpty("hay mas de una categoria en el catalogo");
        var pagina2 = await ListarAsync($"?take=1&cursor={Uri.EscapeDataString(pagina1.SiguienteCursor!)}", ct);

        pagina1.Elementos.Concat(pagina2.Elementos).Select(c => c.Id).Should().Equal(
            referencia.Elementos.Select(c => c.Id),
            "el siguienteCursor de la pagina 1 continua exactamente en la categoria siguiente del orden por Id");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCategoriasDeEtiquetas_Retorna400_CuandoElCursorNoEsValido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync($"{RutaCatalogo}?cursor=no-es-un-cursor", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
