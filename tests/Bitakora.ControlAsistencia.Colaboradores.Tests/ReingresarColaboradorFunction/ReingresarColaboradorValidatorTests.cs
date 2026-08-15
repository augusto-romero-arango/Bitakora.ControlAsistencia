// Issue #350: validacion de forma del comando ReingresarColaborador en el borde (CA-6).
// Patron de referencia: TerminarVinculacionValidatorTests (ValidateAsync + verificacion de
// PropertyName en resultado.Errors).
//
// Issue #387: CodigoColaborador debe ser URL-safe -- set permitido: unreserved characters de
// RFC 3986 seccion 2.3 (A-Z a-z 0-9 - . _ ~), el mismo set que las Microsoft Azure REST API
// Guidelines fijan para path segments (MEF-ADR-0043 seccion 1). El ":" queda explicitamente fuera
// (reservado como separador de accion). Se RECHAZA (400), nunca se limpia ni normaliza en
// silencio -- a diferencia de Identificacion (#381), el codigo lo asigna la empresa y alterarlo
// cambiaria un dato ajeno.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.ReingresarColaboradorFunction;
using Bitakora.ControlAsistencia.Colaboradores.ReingresarColaboradorFunction.CommandHandler;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ReingresarColaboradorFunction;

public class ReingresarColaboradorValidatorTests
{
    private readonly ReingresarColaboradorValidator _validator = new();

    private static ReingresarColaborador ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: "79543210",
        CodigoColaborador: "COL-002",
        FechaInicio: new DateOnly(2026, 6, 2));

    private Task<ValidationResult> Validar(ReingresarColaborador comando) =>
        _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

    // Camino feliz -- todos los campos correctos
    [Fact]
    public async Task Validar_Aprueba_CuandoTodosLosCamposSonCorrectos()
    {
        var resultado = await Validar(ComandoValido());

        resultado.IsValid.Should().BeTrue();
    }

    // CA-6: TipoIdentificacion vacio produce 400
    [Fact]
    public async Task Validar_RechazaTipoIdentificacion_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { TipoIdentificacion = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(ReingresarColaborador.TipoIdentificacion));
    }

    // CA-6: TipoIdentificacion fuera de la lista cerrada (PILA: CC/CE/TI/PA/PT) produce 400, no 500
    [Fact]
    public async Task Validar_RechazaTipoIdentificacion_CuandoNoEstaEnLaListaCerrada()
    {
        var resultado = await Validar(ComandoValido() with { TipoIdentificacion = "XX" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(ReingresarColaborador.TipoIdentificacion));
    }

    // TipoIdentificacion en minusculas ("cc") DEBE seguir siendo valido -- el validator no juzga
    // formato del codigo de tipo; la normalizacion (trim+MAYUSCULAS) vive ahora dentro de
    // TipoIdentificacion.Desde (issue #371, mismo criterio que los demas validators del dominio).
    [Fact]
    public async Task Validar_Aprueba_CuandoTipoIdentificacionLlegaEnMinusculas()
    {
        var resultado = await Validar(ComandoValido() with { TipoIdentificacion = "cc" });

        resultado.IsValid.Should().BeTrue();
    }

    // CA-6: NumeroIdentificacion vacio produce 400
    [Fact]
    public async Task Validar_RechazaNumeroIdentificacion_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { NumeroIdentificacion = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(ReingresarColaborador.NumeroIdentificacion));
    }

    // CA-6: CodigoColaborador vacio produce 400 -- el reingreso siempre trae su propio codigo
    // transaccional.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(ReingresarColaborador.CodigoColaborador));
    }

    // CA-6: FechaInicio con el valor default de DateOnly produce 400 -- REQUERIDA, sin default del
    // servidor (doctrina bitemporal del BC: el tiempo de los hechos viene del cliente).
    [Fact]
    public async Task Validar_RechazaFechaInicio_CuandoEsDefault()
    {
        var resultado = await Validar(ComandoValido() with { FechaInicio = default });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(ReingresarColaborador.FechaInicio));
    }

    // FechaInicio futura DEBE seguir siendo valida en la capa de forma -- la regla de no-solape es
    // del aggregate (CA-3/CA-4), nunca del validator.
    [Fact]
    public async Task Validar_Aprueba_CuandoFechaInicioEsFutura()
    {
        var resultado = await Validar(ComandoValido() with { FechaInicio = new DateOnly(2030, 1, 1) });

        resultado.IsValid.Should().BeTrue();
    }

    // CA-1 (#387): codigo con caracteres unreserved no alfanumericos (. _ ~ -) sigue siendo valido --
    // el set permitido no se limita a alfanumericos.
    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoColaboradorTieneCaracteresUnreservedNoAlfanumericos()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "a.b_c~2" });

        resultado.IsValid.Should().BeTrue();
    }

    // CA-1 (#387): ejemplo explicito del issue -- guion es unreserved.
    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoColaboradorEsAlfanumericoConGuion()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "EMP-001" });

        resultado.IsValid.Should().BeTrue();
    }

    // CA-2 (#387): ":" esta explicitamente fuera del set permitido -- MEF-ADR-0043 seccion 1 lo
    // reserva como separador de accion (vinculaciones/{codigo}:terminar, #379). Un codigo con ":"
    // haria inparseable esa ruta.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneDosPuntos()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "COL:002" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(ReingresarColaborador.CodigoColaborador));
    }

    // CA-3 (#387): espacio no es unreserved -> 400.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneEspacio()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "COL 002" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(ReingresarColaborador.CodigoColaborador));
    }

    // CA-3 (#387): caracter acentuado no es unreserved -> 400.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneCaracterAcentuado()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "CÓDIGO2" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(ReingresarColaborador.CodigoColaborador));
    }

    // CA-3 (#387): "/" no es unreserved -> 400.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneBarra()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "COL/002" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(ReingresarColaborador.CodigoColaborador));
    }
}
