// Issue #386: tests del endpoint HTTP GET colaboradores/fichas/{id} -- alinea la consulta puntual
// a la forma unica de identidad en la ruta que ya usan los comandos del ciclo de vida del
// colaborador (#376/#377): {id} = Identificacion.ToString() ("CC-79543210"), la misma llave que
// devuelve FichaColaborador.Id (round-trip completo del cliente, MEF-ADR-0037). Reemplaza la ruta
// de dos segmentos {tipoIdentificacion}/{numero} (issue #356): la ruta vieja deja de existir (CA-5,
// verificado por la ausencia de esa firma en este archivo).
//
// CA-3: id de ruta invalido (sin guion, tipo fuera de la lista cerrada PILA, numero vacio tras el
// guion) -> 400 -- parseo tipado unico (Identificacion.Parsear), mismo mecanismo que
// CorregirNombresFunction.FunctionEndpoint (precedente post-#376/#377). Los tres casos se
// cortocircuitan ANTES de tocar Marten -- store y tenantResolver se pasan nulos a proposito, mismo
// patron que ListarFichasColaborador/FunctionEndpointTests.cs: si un cambio futuro moviera la
// validacion DESPUES de abrir la QuerySession, estos tests se pondrian rojos por la razon correcta
// (NullReferenceException en vez de 400), nunca en verde por accidente.
//
// CA-1/CA-2/CA-4 (200 con ficha existente, 404 sin ficha, normalizacion "cc-79543210" resuelve la
// misma ficha que "CC-79543210") dependen de session.LoadAsync contra Marten real -- black-box del
// smoke test, mismo precedente que el propio test-writer de #356 dejo documentado en
// FichaColaboradorRespuestaTests.cs ("el resto de Run... es black-box del smoke test"): IDocumentStore/
// IQuerySession de Marten no se fake-ean a mano (violaria "NUNCA NSubstitute" sin aportar cobertura
// real) y este dominio no usa Testcontainers para unit tests (ADR de smoke tests del proyecto).
//
// Fase roja (test-writer): Run() hoy SOLO lanza NotImplementedException (stub minimo de
// compilacion, ver FunctionEndpoint.cs) -- ninguno de estos tests puede pasar todavia.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.ObtenerFichaColaborador;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ObtenerFichaColaborador;

public class FunctionEndpointTests
{
    // store/tenantResolver nulos a proposito: los tres casos de este archivo deben resolverse
    // ANTES de que el endpoint toque Marten (ver comentario de archivo).
    private static FunctionEndpoint Endpoint() => new(store: null!, tenantResolver: null!);

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // CA-3: id de ruta sin guion -> 400, sin llegar a tocar Marten.
    [Fact]
    public async Task ObtenerFichaColaborador_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var result = await Endpoint().Run(FakeHttpRequest(), "CC79543210", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // CA-3: tipo de identificacion fuera de la lista cerrada PILA -> 400.
    [Fact]
    public async Task ObtenerFichaColaborador_Retorna400_CuandoElTipoDeLaIdentificacionNoEstaEnLaListaCerrada()
    {
        var result = await Endpoint().Run(FakeHttpRequest(), "XX-79543210", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // CA-3: numero vacio tras el guion del {id} de ruta -> 400.
    [Fact]
    public async Task ObtenerFichaColaborador_Retorna400_CuandoElNumeroDeLaIdentificacionQuedaVacio()
    {
        var result = await Endpoint().Run(FakeHttpRequest(), "CC-", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
