// CodigoColaborador debe ser URL-safe: solo unreserved de RFC 3986 seccion 2.3 (A-Z a-z 0-9 - . _ ~).
// ":" queda fuera porque es el separador de accion. Se rechaza (400), nunca se limpia en silencio.

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

    [Fact]
    public async Task Validar_Aprueba_CuandoTodosLosCamposSonCorrectos()
    {
        var resultado = await Validar(BodyValido());

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoEstaVacio()
    {
        var resultado = await Validar(BodyValido() with { CodigoColaborador = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(IniciarVinculacionBody.CodigoColaborador));
    }

    // Requerida, sin default del servidor: el tiempo de los hechos viene del cliente (doctrina bitemporal).
    [Fact]
    public async Task Validar_RechazaFechaInicio_CuandoEsDefault()
    {
        var resultado = await Validar(BodyValido() with { FechaInicio = default });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(IniciarVinculacionBody.FechaInicio));
    }

    // La regla de no-solape es del aggregate, nunca del validator.
    [Fact]
    public async Task Validar_Aprueba_CuandoFechaInicioEsFutura()
    {
        var resultado = await Validar(BodyValido() with { FechaInicio = new DateOnly(2030, 1, 1) });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoSedeEsNull()
    {
        var resultado = await Validar(BodyValido() with { CodigoSede = null });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoSedeTieneUnValorPresente()
    {
        var resultado = await Validar(BodyValido() with { CodigoSede = "BOG" });

        resultado.IsValid.Should().BeTrue();
    }

    // Opcional: ausente es valido, pero presente exige valor.
    [Fact]
    public async Task Validar_RechazaCodigoSede_CuandoEstaVacio()
    {
        var resultado = await Validar(BodyValido() with { CodigoSede = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(IniciarVinculacionBody.CodigoSede));
    }

    [Fact]
    public async Task Validar_RechazaCodigoSede_CuandoEsSoloEspacios()
    {
        var resultado = await Validar(BodyValido() with { CodigoSede = "   " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(IniciarVinculacionBody.CodigoSede));
    }

    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoColaboradorTieneCaracteresUnreservedNoAlfanumericos()
    {
        var resultado = await Validar(BodyValido() with { CodigoColaborador = "a.b_c~2" });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoColaboradorEsAlfanumericoConGuion()
    {
        var resultado = await Validar(BodyValido() with { CodigoColaborador = "EMP-001" });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneDosPuntos()
    {
        var resultado = await Validar(BodyValido() with { CodigoColaborador = "COL:002" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(IniciarVinculacionBody.CodigoColaborador));
    }

    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneEspacio()
    {
        var resultado = await Validar(BodyValido() with { CodigoColaborador = "COL 002" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(IniciarVinculacionBody.CodigoColaborador));
    }

    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneCaracterAcentuado()
    {
        var resultado = await Validar(BodyValido() with { CodigoColaborador = "CÓDIGO2" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(IniciarVinculacionBody.CodigoColaborador));
    }

    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneBarra()
    {
        var resultado = await Validar(BodyValido() with { CodigoColaborador = "COL/002" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(IniciarVinculacionBody.CodigoColaborador));
    }

    // En .NET "$" tambien hace match antes de un "\n" final (a diferencia de "\z"): un patron anclado
    // con "$" aceptaria "COL-002\n", que rompe la URL y habilita CRLF injection en quien lo reenvie.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoTerminaEnSaltoDeLinea()
    {
        var resultado = await Validar(BodyValido() with { CodigoColaborador = "COL-002\n" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(IniciarVinculacionBody.CodigoColaborador));
    }
}
