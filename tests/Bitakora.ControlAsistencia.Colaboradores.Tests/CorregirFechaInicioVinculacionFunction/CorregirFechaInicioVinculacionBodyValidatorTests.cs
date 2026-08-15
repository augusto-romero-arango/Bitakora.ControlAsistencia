// Issue #379 (MEF-ADR-0043 paso 4, CA-4): validacion de forma del body reducido (FechaCorregida)
// en el borde. Reemplaza a CorregirFechaInicioVinculacionValidatorTests (eliminado junto con
// CorregirFechaInicioVinculacionValidator, que vivia en CommandHandler/ y validaba el comando
// completo): TipoIdentificacion/NumeroIdentificacion ya no llegan en el body -- se validan en el
// FunctionEndpoint via Identificacion.Parsear (ver FunctionEndpointTests). Patron de referencia:
// TerminarVinculacionBodyValidatorTests (issue #379).

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

    // Camino feliz -- el unico campo del body es correcto
    [Fact]
    public async Task Validar_Aprueba_CuandoFechaCorregidaEsValida()
    {
        var resultado = await Validar(BodyValido());

        resultado.IsValid.Should().BeTrue();
    }

    // CA-4: FechaCorregida con el valor default de DateOnly produce 400 -- REQUERIDA, sin default
    // del servidor (doctrina bitemporal del BC: el tiempo de los hechos viene del cliente).
    [Fact]
    public async Task Validar_RechazaFechaCorregida_CuandoEsDefault()
    {
        var resultado = await Validar(BodyValido() with { FechaCorregida = default });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CorregirFechaInicioVinculacionBody.FechaCorregida));
    }

    // FechaCorregida futura o pasada DEBE seguir siendo valida en la capa de forma -- las reglas
    // de coherencia interna, no-solape y de codigo son del aggregate, nunca del validator.
    [Fact]
    public async Task Validar_Aprueba_CuandoFechaCorregidaEsFutura()
    {
        var resultado = await Validar(BodyValido() with { FechaCorregida = new DateOnly(2030, 1, 1) });

        resultado.IsValid.Should().BeTrue();
    }
}
