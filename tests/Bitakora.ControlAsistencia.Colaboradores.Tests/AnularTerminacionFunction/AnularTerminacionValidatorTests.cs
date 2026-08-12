// Issue #354: validacion de forma del comando AnularTerminacion en el borde (CA-6).
// Patron de referencia: TerminarVinculacionValidatorTests (ValidateAsync + verificacion de
// PropertyName en resultado.Errors).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction.CommandHandler;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AnularTerminacionFunction;

public class AnularTerminacionValidatorTests
{
    private readonly AnularTerminacionValidator _validator = new();

    private static AnularTerminacion ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: "79543210");

    private Task<ValidationResult> Validar(AnularTerminacion comando) =>
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
            e.PropertyName == nameof(AnularTerminacion.TipoIdentificacion));
    }

    // CA-6: TipoIdentificacion fuera de la lista cerrada (PILA: CC/CE/TI/PA/PT) produce 400, no 500
    [Fact]
    public async Task Validar_RechazaTipoIdentificacion_CuandoNoEstaEnLaListaCerrada()
    {
        var resultado = await Validar(ComandoValido() with { TipoIdentificacion = "XX" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(AnularTerminacion.TipoIdentificacion));
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
            e.PropertyName == nameof(AnularTerminacion.NumeroIdentificacion));
    }
}
