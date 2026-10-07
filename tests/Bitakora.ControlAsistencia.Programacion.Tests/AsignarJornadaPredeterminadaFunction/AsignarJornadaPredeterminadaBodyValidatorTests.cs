using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarJornadaPredeterminadaFunction;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarJornadaPredeterminadaFunction;

public class AsignarJornadaPredeterminadaBodyValidatorTests
{
    private readonly AsignarJornadaPredeterminadaBodyValidator _validador = new();

    [Fact]
    public void AsignarJornadaPredeterminada_Falla_CuandoJornadaIdEsGuidEmpty()
    {
        var resultado = _validador.Validate(new AsignarJornadaPredeterminadaBody(Guid.Empty));
        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle(e => e.PropertyName == nameof(AsignarJornadaPredeterminadaBody.JornadaId));
    }

    [Fact]
    public void AsignarJornadaPredeterminada_EsValido_CuandoJornadaIdTieneValor()
    {
        var resultado = _validador.Validate(new AsignarJornadaPredeterminadaBody(Guid.NewGuid()));
        resultado.IsValid.Should().BeTrue();
        resultado.Errors.Should().BeEmpty();
    }
}
