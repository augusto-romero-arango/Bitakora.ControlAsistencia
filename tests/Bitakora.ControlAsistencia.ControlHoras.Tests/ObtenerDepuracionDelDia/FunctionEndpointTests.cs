// Issue #429: tests del endpoint HTTP GET control-horas/depuraciones/{codigoColaborador}/{fecha}.
// CA-5: fecha con formato invalido -> 400 con mensaje que indique el formato esperado (yyyy-MM-dd).
// Mismo patron que Colaboradores.Tests/ObtenerFichaColaborador/FunctionEndpointTests.cs: store y
// tenantResolver nulos a proposito -- el parseo debe cortocircuitar ANTES de tocar Marten. Si un
// cambio futuro moviera la validacion despues de abrir la QuerySession, este test se pondria rojo
// por la razon correcta (excepcion de referencia nula), nunca en verde por accidente.
//
// CA-6 (404 sin body cuando el stream no existe) y la mitad de comportamiento de CA-7 (que la
// QuerySession efectivamente filtre por el tenant resuelto) dependen de
// session.Events.AggregateStreamAsync contra Marten real -- black-box del smoke test
// (ObtenerDepuracionDelDiaSmokeTests, MEF-ADR-0013), mismo criterio que el precedente de
// ObtenerFichaColaborador documenta para su 200/404. La RESOLUCION por constructor de
// IDocumentStore/ITenantResolver (la mitad de wiring de CA-7) la cubre el test de composicion en
// ComposicionServiciosTests.cs (hermano de MEF-ADR-0029).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ObtenerDepuracionDelDia;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ObtenerDepuracionDelDia;

public class FunctionEndpointTests
{
    // store/tenantResolver nulos a proposito: el 400 debe resolverse antes de tocar Marten.
    private static FunctionEndpoint Endpoint() => new(store: null!, tenantResolver: null!);

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    private static Task<IActionResult> ObtenerAsync(string codigoColaborador, string fecha) =>
        Endpoint().Run(FakeHttpRequest(), codigoColaborador, fecha, CancellationToken.None);

    // El 400 debe llevar mensaje: se afirma el tipo Y que el cuerpo trae texto (MEF-ADR-0037
    // seccion 2, proscribe el BadRequestResult pelado).
    [Fact]
    public async Task ObtenerDepuracionDelDia_Retorna400ConMensaje_CuandoLaFechaNoTieneElFormatoEsperado()
    {
        var resultado = await ObtenerAsync("EMP-001", "24-08-2026");

        resultado.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeOfType<string>()
            .Which.Should().NotBeNullOrWhiteSpace();
    }

    // CA-5, variante: fecha que ni siquiera tiene forma de fecha.
    [Fact]
    public async Task ObtenerDepuracionDelDia_Retorna400ConMensaje_CuandoLaFechaEsTextoArbitrario()
    {
        var resultado = await ObtenerAsync("EMP-001", "no-es-una-fecha");

        resultado.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeOfType<string>()
            .Which.Should().NotBeNullOrWhiteSpace();
    }
}
