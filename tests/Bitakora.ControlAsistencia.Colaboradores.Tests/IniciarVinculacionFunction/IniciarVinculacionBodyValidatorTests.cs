// Issue #378 (MEF-ADR-0043 paso 1): validacion de forma del body reducido (CodigoColaborador +
// FechaInicio) en el borde (CA-3). Reemplaza a ReingresarColaboradorValidatorTests (eliminado junto
// con ReingresarColaboradorValidator, que vivia en CommandHandler/ del comando absorbido, issue
// #350): TipoIdentificacion/NumeroIdentificacion ya no llegan en el body -- se validan en el
// FunctionEndpoint via Identificacion.Parsear (ver FunctionEndpointTests). Patron de referencia:
// CorregirNombresBodyValidatorTests (issue #377).
//
// Issue #387 (heredado): CodigoColaborador debe ser URL-safe -- set permitido: unreserved
// characters de RFC 3986 seccion 2.3 (A-Z a-z 0-9 - . _ ~), el mismo set que las Microsoft Azure
// REST API Guidelines fijan para path segments (MEF-ADR-0043 seccion 1). El ":" queda
// explicitamente fuera (reservado como separador de accion). Se RECHAZA (400), nunca se limpia ni
// normaliza en silencio.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.IniciarVinculacionFunction;

public class IniciarVinculacionBodyValidatorTests
{
    private readonly IniciarVinculacionBodyValidator _validator = new();

    private static IniciarVinculacionBody BodyValido() => new(
        CodigoColaborador: "COL-002",
        FechaInicio: new DateOnly(2026, 6, 2));

    private Task<ValidationResult> Validar(IniciarVinculacionBody body) =>
        _validator.ValidateAsync(body, TestContext.Current.CancellationToken);

    // Camino feliz -- todos los campos correctos
    [Fact]
    public async Task Validar_Aprueba_CuandoTodosLosCamposSonCorrectos()
    {
        var resultado = await Validar(BodyValido());

        resultado.IsValid.Should().BeTrue();
    }

    // CA-3: CodigoColaborador vacio produce 400 -- iniciar una vinculacion siempre trae su propio
    // codigo transaccional.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoEstaVacio()
    {
        var resultado = await Validar(BodyValido() with { CodigoColaborador = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(IniciarVinculacionBody.CodigoColaborador));
    }

    // CA-3: FechaInicio con el valor default de DateOnly produce 400 -- REQUERIDA, sin default del
    // servidor (doctrina bitemporal del BC: el tiempo de los hechos viene del cliente).
    [Fact]
    public async Task Validar_RechazaFechaInicio_CuandoEsDefault()
    {
        var resultado = await Validar(BodyValido() with { FechaInicio = default });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(IniciarVinculacionBody.FechaInicio));
    }

    // FechaInicio futura DEBE seguir siendo valida en la capa de forma -- la regla de no-solape es
    // del aggregate, nunca del validator.
    [Fact]
    public async Task Validar_Aprueba_CuandoFechaInicioEsFutura()
    {
        var resultado = await Validar(BodyValido() with { FechaInicio = new DateOnly(2030, 1, 1) });

        resultado.IsValid.Should().BeTrue();
    }

    // (#387): codigo con caracteres unreserved no alfanumericos (. _ ~ -) sigue siendo valido -- el
    // set permitido no se limita a alfanumericos.
    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoColaboradorTieneCaracteresUnreservedNoAlfanumericos()
    {
        var resultado = await Validar(BodyValido() with { CodigoColaborador = "a.b_c~2" });

        resultado.IsValid.Should().BeTrue();
    }

    // (#387): ejemplo explicito del issue original -- guion es unreserved.
    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoColaboradorEsAlfanumericoConGuion()
    {
        var resultado = await Validar(BodyValido() with { CodigoColaborador = "EMP-001" });

        resultado.IsValid.Should().BeTrue();
    }

    // (#387): ":" esta explicitamente fuera del set permitido -- MEF-ADR-0043 seccion 1 lo reserva
    // como separador de accion (vinculaciones/{codigo}:terminar, #379). Un codigo con ":" haria
    // inparseable esa ruta.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneDosPuntos()
    {
        var resultado = await Validar(BodyValido() with { CodigoColaborador = "COL:002" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(IniciarVinculacionBody.CodigoColaborador));
    }

    // (#387): espacio no es unreserved -> 400.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneEspacio()
    {
        var resultado = await Validar(BodyValido() with { CodigoColaborador = "COL 002" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(IniciarVinculacionBody.CodigoColaborador));
    }

    // (#387): caracter acentuado no es unreserved -> 400.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneCaracterAcentuado()
    {
        var resultado = await Validar(BodyValido() with { CodigoColaborador = "CÓDIGO2" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(IniciarVinculacionBody.CodigoColaborador));
    }

    // (#387): "/" no es unreserved -> 400.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneBarra()
    {
        var resultado = await Validar(BodyValido() with { CodigoColaborador = "COL/002" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(IniciarVinculacionBody.CodigoColaborador));
    }

    // (#387), caso borde del ancla de fin de linea: un salto de linea final tampoco es unreserved
    // -> 400. En .NET el ancla "$" hace match tambien ANTES de un "\n" final (a diferencia de
    // "\z"), asi que un patron anclado con "$" aceptaria "COL-002\n" en silencio -- un valor que
    // rompe la URL igual que un espacio, y ademas habilita CRLF injection en cualquier consumidor
    // que lo reenvie en un header o lo escriba en un log.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoTerminaEnSaltoDeLinea()
    {
        var resultado = await Validar(BodyValido() with { CodigoColaborador = "COL-002\n" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(IniciarVinculacionBody.CodigoColaborador));
    }
}
