using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaSemanalFunction;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarJornadaAPlantillaSemanalFunction;

public class AsignarJornadaAPlantillaSemanalBodyValidatorTests
{
    private readonly AsignarJornadaAPlantillaSemanalBodyValidator _validator = new();

    [Fact]
    public void Validar_Falla_CuandoLaJornadaIdEstaVacia()
    {
        var resultado = _validator.Validate(new AsignarJornadaAPlantillaSemanalBody(Guid.Empty));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("JornadaId");
    }

    [Fact]
    public void Validar_Pasa_CuandoLaJornadaIdTieneValor()
    {
        var resultado = _validator.Validate(new AsignarJornadaAPlantillaSemanalBody(Guid.CreateVersion7()));

        resultado.IsValid.Should().BeTrue();
        resultado.Errors.Should().BeEmpty();
    }
}
