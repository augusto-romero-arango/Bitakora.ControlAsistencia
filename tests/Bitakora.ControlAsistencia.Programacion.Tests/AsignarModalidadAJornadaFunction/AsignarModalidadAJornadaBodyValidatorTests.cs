using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarModalidadAJornadaFunction;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarModalidadAJornadaFunction;

public class AsignarModalidadAJornadaBodyValidatorTests
{
    private readonly AsignarModalidadAJornadaBodyValidator _validator = new();

    private async Task<FluentValidation.Results.ValidationResult> Validar(string modalidad) =>
        await _validator.ValidateAsync(new AsignarModalidadAJornadaBody(modalidad), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Body_EsValido_CuandoLaModalidadEstaEnElCatalogo()
    {
        (await Validar("TurnosSucesivos")).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Body_EsValido_CuandoLaModalidadViene_EnMinusculas()
    {
        (await Validar("flexible")).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Body_EsInvalido_CuandoLaModalidadNoEsReconocida()
    {
        (await Validar("Teletrabajo")).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Body_EsInvalido_CuandoLaModalidadEstaVacia()
    {
        (await Validar("")).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Body_EsInvalido_CuandoLaModalidadEsNula()
    {
        (await Validar(null!)).IsValid.Should().BeFalse();
    }
}
