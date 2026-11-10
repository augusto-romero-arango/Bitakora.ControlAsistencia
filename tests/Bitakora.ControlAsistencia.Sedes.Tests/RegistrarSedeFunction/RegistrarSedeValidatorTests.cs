// Codigo debe ser URL-safe: unreserved characters de RFC 3986 seccion 2.3 (A-Z a-z 0-9 - . _ ~), el
// set de path segments de MEF-ADR-0043 seccion 1. Se RECHAZA (400), nunca se limpia ni normaliza.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.RegistrarSedeFunction;
using Bitakora.ControlAsistencia.Sedes.RegistrarSedeFunction.CommandHandler;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Sedes.Tests.RegistrarSedeFunction;

public class RegistrarSedeValidatorTests
{
    private readonly RegistrarSedeValidator _validator = new();

    private static RegistrarSede ComandoValido() =>
        new("SEDE-001", "Sede Principal", "Bogota", "Calle 100 # 10-20");

    private Task<ValidationResult> Validar(RegistrarSede comando) =>
        _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Validar_Aprueba_CuandoTodosLosCamposSonCorrectos()
    {
        var resultado = await Validar(ComandoValido());

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_RechazaCodigo_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { Codigo = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarSede.Codigo));
    }

    [Fact]
    public async Task Validar_RechazaNombre_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { Nombre = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarSede.Nombre));
    }

    [Fact]
    public async Task Validar_Aprueba_CuandoCiudadEsNull()
    {
        var resultado = await Validar(ComandoValido() with { Ciudad = null });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_Aprueba_CuandoDireccionEsNull()
    {
        var resultado = await Validar(ComandoValido() with { Direccion = null });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoTieneCaracteresUnreservedNoAlfanumericos()
    {
        var resultado = await Validar(ComandoValido() with { Codigo = "a.b_c~2" });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoEsAlfanumericoConGuion()
    {
        var resultado = await Validar(ComandoValido() with { Codigo = "SEDE-001" });

        resultado.IsValid.Should().BeTrue();
    }

    // CA-ADR-0031 reserva ":" como separador de la anatomia del stream ("s:{codigo}").
    [Fact]
    public async Task Validar_RechazaCodigo_CuandoContieneDosPuntos()
    {
        var resultado = await Validar(ComandoValido() with { Codigo = "SEDE:001" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarSede.Codigo));
    }

    [Fact]
    public async Task Validar_RechazaCodigo_CuandoContieneEspacio()
    {
        var resultado = await Validar(ComandoValido() with { Codigo = "SEDE 001" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarSede.Codigo));
    }

    [Fact]
    public async Task Validar_RechazaCodigo_CuandoContieneCaracterAcentuado()
    {
        var resultado = await Validar(ComandoValido() with { Codigo = "SÉDE1" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarSede.Codigo));
    }

    [Fact]
    public async Task Validar_RechazaCodigo_CuandoContieneBarra()
    {
        var resultado = await Validar(ComandoValido() with { Codigo = "SEDE/001" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarSede.Codigo));
    }

    // En .NET el ancla "$" hace match tambien ANTES de un "\n" final (a diferencia de "\z").
    [Fact]
    public async Task Validar_RechazaCodigo_CuandoTerminaEnSaltoDeLinea()
    {
        var resultado = await Validar(ComandoValido() with { Codigo = "SEDE-001\n" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarSede.Codigo));
    }
}
