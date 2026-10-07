using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AsignarJornadaFunction;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarJornadaFunction;

public class AsignarJornadaBodyValidatorTests
{
    private readonly AsignarJornadaBodyValidator _validator = new();

    [Fact]
    public async Task Validar_RechazaJornadaId_CuandoEsGuidVacio()
    {
        var resultado = await _validator.ValidateAsync(new AsignarJornadaBody(Guid.Empty));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(AsignarJornadaBody.JornadaId));
    }

    [Fact]
    public async Task Validar_ApruebaJornadaId_CuandoEsGeneral()
    {
        var resultado = await _validator.ValidateAsync(new AsignarJornadaBody(
            Guid.Parse("00000000-0000-4000-8000-000000000001")));

        resultado.IsValid.Should().BeTrue();
    }
}
