// Issue #379 (MEF-ADR-0043 paso 4, CA-2): validacion de forma del body reducido (FechaEfectiva) en
// el borde. Reemplaza a TerminarVinculacionValidatorTests (eliminado junto con
// TerminarVinculacionValidator, que vivia en CommandHandler/ y validaba el comando completo):
// TipoIdentificacion/NumeroIdentificacion ya no llegan en el body -- se validan en el
// FunctionEndpoint via Identificacion.Parsear (ver FunctionEndpointTests). Patron de referencia:
// CorregirNombresBodyValidatorTests (issue #377) / IniciarVinculacionBodyValidatorTests (issue #378).

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

    // Camino feliz -- el unico campo del body es correcto
    [Fact]
    public async Task Validar_Aprueba_CuandoFechaEfectivaEsValida()
    {
        var resultado = await Validar(BodyValido());

        resultado.IsValid.Should().BeTrue();
    }

    // CA-2: FechaEfectiva con el valor default de DateOnly produce 400 -- REQUERIDA, sin default
    // del servidor (doctrina bitemporal del BC: el tiempo de los hechos viene del cliente).
    [Fact]
    public async Task Validar_RechazaFechaEfectiva_CuandoEsDefault()
    {
        var resultado = await Validar(BodyValido() with { FechaEfectiva = default });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(TerminarVinculacionBody.FechaEfectiva));
    }

    // FechaEfectiva futura DEBE seguir siendo valida en la capa de forma -- las reglas de
    // coherencia interna y de codigo son del aggregate, nunca del validator.
    [Fact]
    public async Task Validar_Aprueba_CuandoFechaEfectivaEsFutura()
    {
        var resultado = await Validar(BodyValido() with { FechaEfectiva = new DateOnly(2030, 1, 1) });

        resultado.IsValid.Should().BeTrue();
    }
}
