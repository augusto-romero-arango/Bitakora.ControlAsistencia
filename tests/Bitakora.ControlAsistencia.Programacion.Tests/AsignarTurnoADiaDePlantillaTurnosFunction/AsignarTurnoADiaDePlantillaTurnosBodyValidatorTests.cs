using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaTurnosFunction;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarTurnoADiaDePlantillaTurnosFunction;

public class AsignarTurnoADiaDePlantillaTurnosBodyValidatorTests
{
    private readonly AsignarTurnoADiaDePlantillaTurnosBodyValidator _validator = new();

    [Fact]
    public void Validar_Falla_CuandoElTurnoIdEstaVacio()
    {
        var resultado = _validator.Validate(new AsignarTurnoADiaDePlantillaTurnosBody(Guid.Empty));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("TurnoId");
    }

    [Fact]
    public void Validar_Pasa_CuandoElTurnoIdTieneValor()
    {
        var resultado = _validator.Validate(new AsignarTurnoADiaDePlantillaTurnosBody(Guid.CreateVersion7()));

        resultado.IsValid.Should().BeTrue();
        resultado.Errors.Should().BeEmpty();
    }
}
