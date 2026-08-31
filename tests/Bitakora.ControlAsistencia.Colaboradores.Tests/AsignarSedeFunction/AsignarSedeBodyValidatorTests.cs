using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarSedeFunction;

public class AsignarSedeBodyValidatorTests
{
    private readonly AsignarSedeBodyValidator _validator = new();

    private static AsignarSedeBody BodyValido() => new("BOG");

    private Task<ValidationResult> Validar(AsignarSedeBody body) =>
        _validator.ValidateAsync(body, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoSedeNoEsVacio()
    {
        var resultado = await Validar(BodyValido());

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_RechazaCodigoSede_CuandoEstaVacio()
    {
        var resultado = await Validar(BodyValido() with { CodigoSede = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(AsignarSedeBody.CodigoSede));
    }

    // NotEmpty rechaza tambien whitespace: es la unica guarda de forma que le queda a CodigoSede,
    // porque deliberadamente no se valida charset ni existencia contra el maestro de sedes.
    [Fact]
    public async Task Validar_RechazaCodigoSede_CuandoEsSoloEspacios()
    {
        var resultado = await Validar(BodyValido() with { CodigoSede = "   " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(AsignarSedeBody.CodigoSede));
    }
}
