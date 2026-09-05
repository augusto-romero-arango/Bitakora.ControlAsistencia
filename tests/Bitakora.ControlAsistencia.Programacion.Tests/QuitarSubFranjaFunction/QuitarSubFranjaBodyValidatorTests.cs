using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;
using Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.Tests.QuitarSubFranjaFunction;

public class QuitarSubFranjaBodyValidatorTests
{
    private readonly IValidator<QuitarSubFranjaBody> _validator = new QuitarSubFranjaBodyValidator();

    private static QuitarSubFranjaBody Body(string tipo) =>
        new(new TimeOnly(22, 0), tipo, new TimeOnly(2, 0));

    [Fact]
    public async Task DebeSerValido_CuandoTipoEsDescansoEnMinusculas()
    {
        var resultado = await _validator.ValidateAsync(
            Body("descanso"), TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task DebeSerValido_CuandoTipoEsExtraConMayusculaInicial()
    {
        var resultado = await _validator.ValidateAsync(
            Body("Extra"), TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task DebeRechazar_CuandoTipoNoEsDescansoNiExtra()
    {
        var resultado = await _validator.ValidateAsync(
            Body("pausa"), TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should()
            .Contain(e => e.ErrorMessage.Contains(AgregarSubFranjaBodyValidator.Mensajes.TipoDesconocido));
    }

    [Fact]
    public async Task DebeRechazar_CuandoTipoEstaVacio()
    {
        var resultado = await _validator.ValidateAsync(
            Body(string.Empty), TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should()
            .Contain(e => e.ErrorMessage.Contains(AgregarSubFranjaBodyValidator.Mensajes.TipoDesconocido));
    }
}
