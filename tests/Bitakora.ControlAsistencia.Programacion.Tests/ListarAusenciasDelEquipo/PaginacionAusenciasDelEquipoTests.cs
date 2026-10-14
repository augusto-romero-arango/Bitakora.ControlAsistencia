using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ListarAusenciasDelEquipo;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ListarAusenciasDelEquipo;

// La unidad de pagina es el colaborador (con sus ausencias completas); orden (NombreCompleto, Codigo).
public class PaginacionAusenciasDelEquipoTests
{
    private static DateOnly Oct(int dia) => new(2026, 10, dia);

    private static readonly RangoAplicado Semana = new(Oct(19), false);

    private static AusenciaVigente Vista(string id, string codigo, string nombre, string motivo) =>
        new(Guid.Parse(id), codigo, nombre, motivo, [new TramoDeAusencia(Oct(14), Oct(15))], Oct(14), Oct(15));

    private static readonly AusenciaVigente Ana = Vista("019600b0-0000-7000-8000-000000000001", "EMP-001", "Ana Ramirez", "Vacaciones");
    private static readonly AusenciaVigente AnaOtra = Vista("019600b0-0000-7000-8000-000000000004", "EMP-001", "Ana Ramirez", "LicenciaRemunerada");
    private static readonly AusenciaVigente Luis = Vista("019600b0-0000-7000-8000-000000000002", "EMP-002", "Luis Perez", "IncapacidadMedica");
    private static readonly AusenciaVigente Zoe = Vista("019600b0-0000-7000-8000-000000000003", "EMP-003", "Zoe Diaz", "Vacaciones");

    [Fact]
    public void Componer_NoTraeCursor_CuandoNoHayTake()
    {
        var lista = ListaAusenciasDelEquipo.Componer(Oct(13), Semana, [Zoe, Luis, Ana]);

        lista.Elementos.Select(e => e.CodigoColaborador).Should().Equal("EMP-001", "EMP-002", "EMP-003");
        lista.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_PaginaPorColaboradorConSusAusenciasCompletas_CuandoHayTake()
    {
        var lista = ListaAusenciasDelEquipo.Componer(Oct(13), Semana, [Zoe, Luis, Ana, AnaOtra], take: 1);

        lista.Elementos.Should().ContainSingle().Which.CodigoColaborador.Should().Be("EMP-001");
        lista.Elementos[0].Ausencias.Should().HaveCount(2);
        CursorAusenciasEquipo.Decodificar(lista.SiguienteCursor!)
            .Should().Be(new CursorAusenciasEquipo("Ana Ramirez", "EMP-001"));
    }

    [Fact]
    public void Componer_ContinuaDespuesDelCursor_CuandoSeEnviaElCursorDeLaPaginaAnterior()
    {
        var cursor = new CursorAusenciasEquipo("Ana Ramirez", "EMP-001");

        var lista = ListaAusenciasDelEquipo.Componer(Oct(13), Semana, [Zoe, Luis, Ana], take: 1, cursor: cursor);

        lista.Elementos.Should().ContainSingle().Which.CodigoColaborador.Should().Be("EMP-002");
        CursorAusenciasEquipo.Decodificar(lista.SiguienteCursor!)
            .Should().Be(new CursorAusenciasEquipo("Luis Perez", "EMP-002"));
    }

    [Fact]
    public void Componer_NoTraeCursor_CuandoLaUltimaPaginaCierraElResultado()
    {
        var cursor = new CursorAusenciasEquipo("Luis Perez", "EMP-002");

        var lista = ListaAusenciasDelEquipo.Componer(Oct(13), Semana, [Zoe, Luis, Ana], take: 1, cursor: cursor);

        lista.Elementos.Should().ContainSingle().Which.CodigoColaborador.Should().Be("EMP-003");
        lista.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_ConservaLaVentanaAplicada_CuandoHayPaginacion()
    {
        var lista = ListaAusenciasDelEquipo.Componer(Oct(13), new RangoAplicado(Oct(19), true), [Ana, Luis], take: 1);

        lista.Desde.Should().Be(Oct(13));
        lista.Hasta.Should().Be(Oct(19));
        lista.RangoRecortado.Should().BeTrue();
    }

    [Fact]
    public void Decodificar_RetornaNull_CuandoElTextoNoEsUnCursor()
    {
        CursorAusenciasEquipo.Decodificar("%%no-es-un-cursor").Should().BeNull();
    }

    private static async Task<int?> EjecutarAsync(string body)
    {
        var context = new DefaultHttpContext();
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = bytes.Length;
        context.Request.Body = new MemoryStream(bytes);
        var resultado = await new FunctionEndpoint(null!, null!).Run(context.Request, CancellationToken.None);
        return resultado.Should().BeAssignableTo<IStatusCodeActionResult>().Subject.StatusCode;
    }

    [Fact]
    public async Task ListarAusenciasDelEquipo_Retorna422_CuandoTakeEsMayorA200()
    {
        (await EjecutarAsync("""{"desde":"2026-10-13","hasta":"2026-10-19","take":201}"""))
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarAusenciasDelEquipo_Retorna422_CuandoTakeNoEsPositivo()
    {
        (await EjecutarAsync("""{"desde":"2026-10-13","hasta":"2026-10-19","take":0}"""))
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarAusenciasDelEquipo_Retorna422_CuandoElCursorEsInvalido()
    {
        (await EjecutarAsync("""{"desde":"2026-10-13","hasta":"2026-10-19","cursor":"%%no-es-un-cursor"}"""))
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
    }
}
