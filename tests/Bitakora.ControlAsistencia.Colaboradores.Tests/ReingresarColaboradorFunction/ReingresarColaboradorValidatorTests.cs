// Issue #350: validacion de forma del comando ReingresarColaborador en el borde (CA-6).
// Patron de referencia: TerminarVinculacionValidatorTests (ValidateAsync + verificacion de
// PropertyName en resultado.Errors).

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

    // TipoIdentificacion en minusculas ("cc") DEBE seguir siendo valido -- la normalizacion de
    // entrada (trim+MAYUSCULAS antes de TipoIdentificacion.Desde) es responsabilidad del borde, no
    // un rechazo (mismo criterio que los demas validators del dominio).
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
}
