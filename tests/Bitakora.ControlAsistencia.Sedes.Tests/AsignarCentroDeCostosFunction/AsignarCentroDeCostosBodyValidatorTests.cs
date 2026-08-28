using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.AsignarCentroDeCostosFunction;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Sedes.Tests.AsignarCentroDeCostosFunction;

public class AsignarCentroDeCostosBodyValidatorTests
{
    private readonly AsignarCentroDeCostosBodyValidator _validator = new();

    private Task<ValidationResult> Validar(AsignarCentroDeCostosBody body) =>
        _validator.ValidateAsync(body, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Validar_Aprueba_CuandoElCentroDeCostosNoEstaVacio()
    {
        var resultado = await Validar(new AsignarCentroDeCostosBody("CC-100"));

        resultado.IsValid.Should().BeTrue();
    }

    // CA-5
    [Fact]
    public async Task Validar_RechazaCentroDeCostos_CuandoEstaVacio()
    {
        var resultado = await Validar(new AsignarCentroDeCostosBody(""));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(
            e => e.PropertyName == nameof(AsignarCentroDeCostosBody.CentroDeCostos));
    }
}
