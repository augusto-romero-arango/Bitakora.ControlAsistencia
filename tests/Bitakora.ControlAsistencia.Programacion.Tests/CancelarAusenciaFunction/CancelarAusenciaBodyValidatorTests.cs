using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CancelarAusenciaFunction;
using Bitakora.ControlAsistencia.Programacion.CancelarAusenciaFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CancelarAusenciaFunction;

public class CancelarAusenciaBodyValidatorTests
{
    private readonly CancelarAusenciaBodyValidator _validator = new();

    private async Task<FluentValidation.Results.ValidationResult> Validar(CancelarAusenciaBody body) =>
        await _validator.ValidateAsync(body, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Body_EsValido_CuandoTraeAlMenosUnaFecha()
    {
        var resultado = await Validar(new CancelarAusenciaBody([new DateOnly(2026, 10, 20)]));

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Body_EsValido_CuandoLasFechasVienenRepetidas()
    {
        var fecha = new DateOnly(2026, 10, 20);

        var resultado = await Validar(new CancelarAusenciaBody([fecha, fecha]));

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Body_EsInvalido_CuandoLaListaDeFechasEstaVacia()
    {
        var resultado = await Validar(new CancelarAusenciaBody([]));

        resultado.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Body_EsInvalido_CuandoLaListaDeFechasEstaAusente()
    {
        var resultado = await Validar(new CancelarAusenciaBody(null));

        resultado.IsValid.Should().BeFalse();
    }
}
