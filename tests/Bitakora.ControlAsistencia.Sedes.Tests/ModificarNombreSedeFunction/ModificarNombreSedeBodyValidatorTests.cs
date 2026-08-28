// Issue #457 (CA-2): validacion de forma del body de ModificarNombreSede en el borde
// (MEF-ADR-0004 capa 1 -> 400 BadRequest).

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

    // CA-2: nombre vacio produce 400
    [Fact]
    public async Task Validar_RechazaNombre_CuandoEstaVacio()
    {
        var resultado = await Validar(new ModificarNombreSedeBody(""));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(
            e => e.PropertyName == nameof(ModificarNombreSedeBody.Nombre));
    }
}
