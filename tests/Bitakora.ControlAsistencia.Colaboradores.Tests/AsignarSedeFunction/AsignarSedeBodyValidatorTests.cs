// Issue #465: validacion de forma del body { "codigoSede": "..." } en el borde (400 via
// RequestValidator). Patron de referencia: AsignarEtiquetaBodyValidatorTests.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarSedeFunction;

public class AsignarSedeBodyValidatorTests
{
    private readonly AsignarSedeBodyValidator _validator = new();

    private static AsignarSedeBody BodyValido() => new("BOG");

    private Task<ValidationResult> Validar(AsignarSedeBody body) =>
        _validator.ValidateAsync(body, TestContext.Current.CancellationToken);

    // Camino feliz -- CodigoSede no vacio
    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoSedeNoEsVacio()
    {
        var resultado = await Validar(BodyValido());

        resultado.IsValid.Should().BeTrue();
    }

    // CodigoSede vacio produce 400
    [Fact]
    public async Task Validar_RechazaCodigoSede_CuandoEstaVacio()
    {
        var resultado = await Validar(BodyValido() with { CodigoSede = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(AsignarSedeBody.CodigoSede));
    }

    // NotEmpty de FluentValidation rechaza tambien whitespace, no solo la cadena vacia -- unica
    // guarda de forma que le queda a CodigoSede (no se valida charset ni existencia contra el
    // maestro de sedes, decision de refinamiento: el filtro de sedes activas es del cliente).
    [Fact]
    public async Task Validar_RechazaCodigoSede_CuandoEsSoloEspacios()
    {
        var resultado = await Validar(BodyValido() with { CodigoSede = "   " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(AsignarSedeBody.CodigoSede));
    }
}
