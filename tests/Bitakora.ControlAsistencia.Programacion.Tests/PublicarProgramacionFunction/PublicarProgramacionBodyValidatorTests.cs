using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.PublicarProgramacionFunction;
using Bitakora.ControlAsistencia.Programacion.PublicarProgramacionFunction.CommandHandler;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Programacion.Tests.PublicarProgramacionFunction;

public class PublicarProgramacionBodyValidatorTests
{
    private readonly PublicarProgramacionBodyValidator _validator = new();

    private async Task<ValidationResult> Validar(DateOnly? desde, DateOnly? hasta) =>
        await _validator.ValidateAsync(new PublicarProgramacionBody(desde, hasta), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Body_EsValido_CuandoElRangoEsDeUnDia()
    {
        var resultado = await Validar(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5));

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Body_EsValido_CuandoElRangoCruzaMeses()
    {
        var resultado = await Validar(new DateOnly(2026, 10, 28), new DateOnly(2026, 11, 3));

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Body_EsValido_CuandoElRangoCubreExactamente366Dias()
    {
        var resultado = await Validar(new DateOnly(2028, 1, 1), new DateOnly(2028, 12, 31));

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Body_EsInvalido_CuandoElRangoCubre367Dias()
    {
        var resultado = await Validar(new DateOnly(2028, 1, 1), new DateOnly(2029, 1, 1));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains(PublicarProgramacionBodyValidator.Mensajes.RangoExcedeElMaximo));
    }

    [Fact]
    public async Task Body_EsInvalido_CuandoFaltaDesde()
    {
        var resultado = await Validar(null, new DateOnly(2026, 10, 5));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains(PublicarProgramacionBodyValidator.Mensajes.DesdeRequerido));
    }

    [Fact]
    public async Task Body_EsInvalido_CuandoFaltaHasta()
    {
        var resultado = await Validar(new DateOnly(2026, 10, 5), null);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains(PublicarProgramacionBodyValidator.Mensajes.HastaRequerido));
    }

    [Fact]
    public async Task Body_EsInvalido_CuandoDesdeEsPosteriorAHasta()
    {
        var resultado = await Validar(new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 5));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains(PublicarProgramacionBodyValidator.Mensajes.DesdeMayorQueHasta));
    }
}
