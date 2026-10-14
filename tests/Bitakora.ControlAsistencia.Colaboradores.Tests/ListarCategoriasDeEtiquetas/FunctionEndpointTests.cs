using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.ListarCategoriasDeEtiquetas;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ListarCategoriasDeEtiquetas;

public class FunctionEndpointTests
{
    private static FunctionEndpoint Endpoint() => new(store: null!, tenantContext: null!);

    private static HttpRequest Request(string query)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(query);
        return context.Request;
    }

    private static CategoriaDeEtiquetas Categoria(string id) => new(id, id, []);

    // --- CA-4: entrada invalida -> 400 (GET) antes de abrir la QuerySession ---

    [Fact]
    public async Task ListarCategoriasDeEtiquetas_Retorna400_CuandoTakeEsCero()
    {
        var resultado = await Endpoint().Run(Request("?take=0"), CancellationToken.None);

        resultado.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ListarCategoriasDeEtiquetas_Retorna400_CuandoTakeSuperaElMaximoDe200()
    {
        var resultado = await Endpoint().Run(Request("?take=201"), CancellationToken.None);

        resultado.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ListarCategoriasDeEtiquetas_Retorna400_CuandoTakeNoEsNumerico()
    {
        var resultado = await Endpoint().Run(Request("?take=abc"), CancellationToken.None);

        resultado.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ListarCategoriasDeEtiquetas_Retorna400_CuandoElCursorEsIndescifrable()
    {
        var resultado = await Endpoint().Run(Request("?cursor=esto-no-es-un-cursor"), CancellationToken.None);

        resultado.Should().BeOfType<BadRequestObjectResult>();
    }

    // --- Cursor opaco ---

    [Fact]
    public void CursorCategoria_RecuperaLaClaveKeyset_CuandoSeDecodificaLoCodificado()
    {
        var cursor = CursorCategoria.Codificar("area/ubicación");

        CursorCategoria.TryDecodificar(cursor, out var id).Should().BeTrue();
        id.Should().Be("area/ubicación");
        cursor.Should().MatchRegex("^[A-Za-z0-9_-]+$");
    }

    [Fact]
    public void CursorCategoria_NoDecodifica_CuandoElTextoEsIndescifrable() =>
        CursorCategoria.TryDecodificar("esto no es base64url!", out _).Should().BeFalse();

    // --- Sobre { elementos, siguienteCursor } (CA-1) ---

    [Fact]
    public void Componer_DevuelveTodoSinCursor_CuandoNoHayTake()
    {
        var lista = FunctionEndpoint.Componer([Categoria("area"), Categoria("cargo")], take: null);

        lista.Elementos.Select(c => c.Id).Should().Equal("area", "cargo");
        lista.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_RecortaALaPaginaYApuntaALaUltimaFilaEntregada_CuandoHayUnaFilaDeMas()
    {
        var lista = FunctionEndpoint.Componer([Categoria("area"), Categoria("cargo")], take: 1);

        lista.Elementos.Select(c => c.Id).Should().Equal("area");
        CursorCategoria.TryDecodificar(lista.SiguienteCursor!, out var id).Should().BeTrue();
        id.Should().Be("area");
    }

    [Fact]
    public void Componer_NoTraeCursor_CuandoLaPaginaNoTieneMasFilas()
    {
        var lista = FunctionEndpoint.Componer([Categoria("area")], take: 1);

        lista.Elementos.Should().HaveCount(1);
        lista.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_DevuelveListaVacia_CuandoNoHayCategorias()
    {
        var lista = FunctionEndpoint.Componer([], take: null);

        lista.Elementos.Should().BeEmpty();
        lista.SiguienteCursor.Should().BeNull();
    }
}
