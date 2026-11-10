using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.TerminarVinculacionFunction;

public class TerminarVinculacionBodyValidatorTests
{
    private readonly TerminarVinculacionBodyValidator _validator = new();

    private static TerminarVinculacionBody BodyValido() => new(FechaEfectiva: new DateOnly(2026, 6, 1));

    private Task<ValidationResult> Validar(TerminarVinculacionBody body) =>
        _validator.ValidateAsync(body, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Validar_Aprueba_CuandoFechaEfectivaEsValida()
    {
        var resultado = await Validar(BodyValido());

        resultado.IsValid.Should().BeTrue();
    }

    // Requerida, sin default del servidor: el tiempo de los hechos viene del cliente (doctrina bitemporal).
    [Fact]
    public async Task Validar_RechazaFechaEfectiva_CuandoEsDefault()
    {
        var resultado = await Validar(BodyValido() with { FechaEfectiva = default });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(TerminarVinculacionBody.FechaEfectiva));
    }

    // Las reglas de coherencia interna y de codigo son del aggregate, nunca del validator.
    [Fact]
    public async Task Validar_Aprueba_CuandoFechaEfectivaEsFutura()
    {
        var resultado = await Validar(BodyValido() with { FechaEfectiva = new DateOnly(2030, 1, 1) });

        resultado.IsValid.Should().BeTrue();
    }
}
