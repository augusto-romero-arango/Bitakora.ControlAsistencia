using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaTurnosFunction;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarJornadaAPlantillaTurnosFunction;

public class AsignarJornadaAPlantillaTurnosBodyValidatorTests
{
    private readonly AsignarJornadaAPlantillaTurnosBodyValidator _validator = new();

    [Fact]
    public void Validar_Falla_CuandoLaJornadaIdEstaVacia()
    {
        var resultado = _validator.Validate(new AsignarJornadaAPlantillaTurnosBody(Guid.Empty));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("JornadaId");
    }

    [Fact]
    public void Validar_Pasa_CuandoLaJornadaIdTieneValor()
    {
        var resultado = _validator.Validate(new AsignarJornadaAPlantillaTurnosBody(Guid.CreateVersion7()));

        resultado.IsValid.Should().BeTrue();
        resultado.Errors.Should().BeEmpty();
    }
}
