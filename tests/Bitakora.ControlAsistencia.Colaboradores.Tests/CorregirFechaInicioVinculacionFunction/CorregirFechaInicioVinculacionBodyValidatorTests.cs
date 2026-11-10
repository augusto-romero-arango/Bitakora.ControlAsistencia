using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirFechaInicioVinculacionFunction;

public class CorregirFechaInicioVinculacionBodyValidatorTests
{
    private readonly CorregirFechaInicioVinculacionBodyValidator _validator = new();

    private static CorregirFechaInicioVinculacionBody BodyValido() =>
        new(FechaCorregida: new DateOnly(2026, 1, 10));

    private Task<ValidationResult> Validar(CorregirFechaInicioVinculacionBody body) =>
        _validator.ValidateAsync(body, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Validar_Aprueba_CuandoFechaCorregidaEsValida()
    {
        var resultado = await Validar(BodyValido());

        resultado.IsValid.Should().BeTrue();
    }

    // Requerida, sin default del servidor: el tiempo de los hechos viene del cliente (doctrina bitemporal).
    [Fact]
    public async Task Validar_RechazaFechaCorregida_CuandoEsDefault()
    {
        var resultado = await Validar(BodyValido() with { FechaCorregida = default });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CorregirFechaInicioVinculacionBody.FechaCorregida));
    }

    // Las reglas de coherencia, no-solape y codigo son del aggregate, nunca del validator.
    [Fact]
    public async Task Validar_Aprueba_CuandoFechaCorregidaEsFutura()
    {
        var resultado = await Validar(BodyValido() with { FechaCorregida = new DateOnly(2030, 1, 1) });

        resultado.IsValid.Should().BeTrue();
    }
}
