using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ModificarNombreSedeFunction;

public class ModificarNombreSedeBodyValidatorTests
{
    private readonly ModificarNombreSedeBodyValidator _validator = new();

    private Task<ValidationResult> Validar(ModificarNombreSedeBody body) =>
        _validator.ValidateAsync(body, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Validar_Aprueba_CuandoNombreEsCorrecto()
    {
        var resultado = await Validar(new ModificarNombreSedeBody("Sede Renombrada"));

        resultado.IsValid.Should().BeTrue();
    }

    // CA-2
    [Fact]
    public async Task Validar_RechazaNombre_CuandoEstaVacio()
    {
        var resultado = await Validar(new ModificarNombreSedeBody(""));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(
            e => e.PropertyName == nameof(ModificarNombreSedeBody.Nombre));
    }
}
