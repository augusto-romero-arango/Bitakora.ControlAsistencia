// Issue #349: validacion de forma del comando TerminarVinculacion en el borde (CA-6).
// Patron de referencia: RegistrarColaboradorValidatorTests (ValidateAsync + verificacion de
// PropertyName en resultado.Errors).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction.CommandHandler;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.TerminarVinculacionFunction;

public class TerminarVinculacionValidatorTests
{
    private readonly TerminarVinculacionValidator _validator = new();

    private static TerminarVinculacion ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: "79543210",
        FechaEfectiva: new DateOnly(2026, 6, 1));

    private Task<ValidationResult> Validar(TerminarVinculacion comando) =>
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
            e.PropertyName == nameof(TerminarVinculacion.TipoIdentificacion));
    }

    // CA-6: TipoIdentificacion fuera de la lista cerrada (PILA: CC/CE/TI/PA/PT) produce 400, no 500
    [Fact]
    public async Task Validar_RechazaTipoIdentificacion_CuandoNoEstaEnLaListaCerrada()
    {
        var resultado = await Validar(ComandoValido() with { TipoIdentificacion = "XX" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(TerminarVinculacion.TipoIdentificacion));
    }

    // TipoIdentificacion en minusculas ("cc") DEBE seguir siendo valido -- el validator no juzga
    // formato del codigo de tipo; la normalizacion (trim+MAYUSCULAS) vive ahora dentro de
    // TipoIdentificacion.Desde (issue #371, mismo criterio que RegistrarColaboradorValidator).
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
            e.PropertyName == nameof(TerminarVinculacion.NumeroIdentificacion));
    }

    // CA-6: FechaEfectiva con el valor default de DateOnly produce 400 -- REQUERIDA, sin default
    // del servidor (doctrina bitemporal del BC: el tiempo de los hechos viene del cliente).
    [Fact]
    public async Task Validar_RechazaFechaEfectiva_CuandoEsDefault()
    {
        var resultado = await Validar(ComandoValido() with { FechaEfectiva = default });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(TerminarVinculacion.FechaEfectiva));
    }

    // CA-2: FechaEfectiva futura DEBE seguir siendo valida -- sin validacion contra el reloj del
    // servidor en ninguna direccion (decision de refinamiento del issue #349).
    [Fact]
    public async Task Validar_Aprueba_CuandoFechaEfectivaEsFutura()
    {
        var resultado = await Validar(ComandoValido() with { FechaEfectiva = new DateOnly(2030, 1, 1) });

        resultado.IsValid.Should().BeTrue();
    }
}
