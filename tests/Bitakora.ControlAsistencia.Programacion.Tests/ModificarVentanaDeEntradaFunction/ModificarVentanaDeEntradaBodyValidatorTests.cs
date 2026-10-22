using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.ModificarVentanaDeEntradaFunction;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ModificarVentanaDeEntradaFunction;

public class ModificarVentanaDeEntradaBodyValidatorTests
{
    private readonly ModificarVentanaDeEntradaBodyValidator _validator = new();

    [Fact]
    public async Task Body_EsValido_CuandoTraeLaAmplitud()
    {
        var resultado = await _validator.ValidateAsync(
            new ModificarVentanaDeEntradaBody(new HorasYMinutosSolicitadas(3, 0)),
            TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Body_EsInvalido_CuandoFaltaLaAmplitud()
    {
        var resultado = await _validator.ValidateAsync(
            new ModificarVentanaDeEntradaBody(null!), TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
    }
}
