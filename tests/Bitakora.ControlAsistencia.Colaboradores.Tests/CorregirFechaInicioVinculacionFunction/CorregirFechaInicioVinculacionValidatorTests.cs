// Issue #352: validacion de forma del comando CorregirFechaInicioVinculacion en el borde (CA-6).
// Patron de referencia: ReingresarColaboradorValidatorTests (ValidateAsync + verificacion de
// PropertyName en resultado.Errors).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction.CommandHandler;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirFechaInicioVinculacionFunction;

public class CorregirFechaInicioVinculacionValidatorTests
{
    private readonly CorregirFechaInicioVinculacionValidator _validator = new();

    private static CorregirFechaInicioVinculacion ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: "79543210",
        FechaCorregida: new DateOnly(2026, 1, 10));

    private Task<ValidationResult> Validar(CorregirFechaInicioVinculacion comando) =>
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
            e.PropertyName == nameof(CorregirFechaInicioVinculacion.TipoIdentificacion));
    }

    // CA-6: TipoIdentificacion fuera de la lista cerrada (PILA: CC/CE/TI/PA/PT) produce 400, no 500
    [Fact]
    public async Task Validar_RechazaTipoIdentificacion_CuandoNoEstaEnLaListaCerrada()
    {
        var resultado = await Validar(ComandoValido() with { TipoIdentificacion = "XX" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CorregirFechaInicioVinculacion.TipoIdentificacion));
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
            e.PropertyName == nameof(CorregirFechaInicioVinculacion.NumeroIdentificacion));
    }

    // CA-6: FechaCorregida con el valor default de DateOnly produce 400 -- REQUERIDA, sin default
    // del servidor (doctrina bitemporal del BC: el tiempo de los hechos viene del cliente).
    [Fact]
    public async Task Validar_RechazaFechaCorregida_CuandoEsDefault()
    {
        var resultado = await Validar(ComandoValido() with { FechaCorregida = default });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CorregirFechaInicioVinculacion.FechaCorregida));
    }

    // FechaCorregida futura o pasada DEBE seguir siendo valida en la capa de forma -- las reglas
    // de coherencia interna y no-solape son del aggregate (CA-2/CA-3), nunca del validator.
    [Fact]
    public async Task Validar_Aprueba_CuandoFechaCorregidaEsFutura()
    {
        var resultado = await Validar(ComandoValido() with { FechaCorregida = new DateOnly(2030, 1, 1) });

        resultado.IsValid.Should().BeTrue();
    }
}
