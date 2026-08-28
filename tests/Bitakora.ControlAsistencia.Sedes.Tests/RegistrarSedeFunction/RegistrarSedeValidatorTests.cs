// Issue #456: validacion de forma del comando RegistrarSede en el borde (CA-3).
// Codigo ademas debe ser URL-safe (CA-4): set permitido: unreserved characters de RFC 3986
// seccion 2.3 (A-Z a-z 0-9 - . _ ~), el mismo set que las Microsoft Azure REST API Guidelines fijan
// para path segments (MEF-ADR-0043 seccion 1). Se RECHAZA (400), nunca se limpia ni normaliza.

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

    // Camino feliz -- todos los campos correctos
    [Fact]
    public async Task Validar_Aprueba_CuandoTodosLosCamposSonCorrectos()
    {
        var resultado = await Validar(ComandoValido());

        resultado.IsValid.Should().BeTrue();
    }

    // CA-3: Codigo vacio produce 400
    [Fact]
    public async Task Validar_RechazaCodigo_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { Codigo = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarSede.Codigo));
    }

    // CA-3: Nombre vacio produce 400
    [Fact]
    public async Task Validar_RechazaNombre_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { Nombre = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarSede.Nombre));
    }

    // CA-2: Ciudad ausente (null) es valida -- campo opcional
    [Fact]
    public async Task Validar_Aprueba_CuandoCiudadEsNull()
    {
        var resultado = await Validar(ComandoValido() with { Ciudad = null });

        resultado.IsValid.Should().BeTrue();
    }

    // CA-2: Direccion ausente (null) es valida -- campo opcional
    [Fact]
    public async Task Validar_Aprueba_CuandoDireccionEsNull()
    {
        var resultado = await Validar(ComandoValido() with { Direccion = null });

        resultado.IsValid.Should().BeTrue();
    }

    // CA-4: caracteres unreserved no alfanumericos (. _ ~ -) siguen siendo validos
    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoTieneCaracteresUnreservedNoAlfanumericos()
    {
        var resultado = await Validar(ComandoValido() with { Codigo = "a.b_c~2" });

        resultado.IsValid.Should().BeTrue();
    }

    // CA-4: ejemplo explicito del issue -- guion es unreserved
    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoEsAlfanumericoConGuion()
    {
        var resultado = await Validar(ComandoValido() with { Codigo = "SEDE-001" });

        resultado.IsValid.Should().BeTrue();
    }

    // CA-4: ":" esta explicitamente fuera del set permitido -- CA-ADR-0031 lo reserva como
    // separador de la anatomia del stream ("s:{codigo}").
    [Fact]
    public async Task Validar_RechazaCodigo_CuandoContieneDosPuntos()
    {
        var resultado = await Validar(ComandoValido() with { Codigo = "SEDE:001" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarSede.Codigo));
    }

    // CA-4: espacio no es unreserved -> 400
    [Fact]
    public async Task Validar_RechazaCodigo_CuandoContieneEspacio()
    {
        var resultado = await Validar(ComandoValido() with { Codigo = "SEDE 001" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarSede.Codigo));
    }

    // CA-4: caracter acentuado no es unreserved -> 400
    [Fact]
    public async Task Validar_RechazaCodigo_CuandoContieneCaracterAcentuado()
    {
        var resultado = await Validar(ComandoValido() with { Codigo = "SÉDE1" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarSede.Codigo));
    }

    // CA-4: "/" no es unreserved -> 400
    [Fact]
    public async Task Validar_RechazaCodigo_CuandoContieneBarra()
    {
        var resultado = await Validar(ComandoValido() with { Codigo = "SEDE/001" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarSede.Codigo));
    }

    // CA-4, caso borde del ancla de fin de linea: un salto de linea final tampoco es unreserved ->
    // 400. En .NET el ancla "$" hace match tambien ANTES de un "\n" final (a diferencia de "\z").
    [Fact]
    public async Task Validar_RechazaCodigo_CuandoTerminaEnSaltoDeLinea()
    {
        var resultado = await Validar(ComandoValido() with { Codigo = "SEDE-001\n" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(RegistrarSede.Codigo));
    }
}
