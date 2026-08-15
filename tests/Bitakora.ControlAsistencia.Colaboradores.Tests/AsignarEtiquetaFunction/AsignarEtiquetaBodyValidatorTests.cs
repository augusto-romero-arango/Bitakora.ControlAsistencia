// Issue #376 (MEF-ADR-0043 paso 2): validacion de forma del body reducido { "valor": "..." } en el
// borde (CA-1). Reemplaza a AsignarEtiquetaValidatorTests (eliminado junto con
// AsignarEtiquetaValidator): TipoIdentificacion/NumeroIdentificacion/Categoria ya no llegan en el
// body -- se validan en el FunctionEndpoint via Identificacion.Parsear (ver FunctionEndpointTests).
// Patron de referencia: AnularTerminacionValidatorTests (ValidateAsync + verificacion de
// PropertyName en resultado.Errors).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarEtiquetaFunction;

public class AsignarEtiquetaBodyValidatorTests
{
    private readonly AsignarEtiquetaBodyValidator _validator = new();

    private static AsignarEtiquetaBody BodyValido() => new("Tecnología");

    private Task<ValidationResult> Validar(AsignarEtiquetaBody body) =>
        _validator.ValidateAsync(body, TestContext.Current.CancellationToken);

    // Camino feliz -- Valor no vacio
    [Fact]
    public async Task Validar_Aprueba_CuandoValorNoEsVacio()
    {
        var resultado = await Validar(BodyValido());

        resultado.IsValid.Should().BeTrue();
    }

    // CA-1: Valor vacio produce 400
    [Fact]
    public async Task Validar_RechazaValor_CuandoEstaVacio()
    {
        var resultado = await Validar(BodyValido() with { Valor = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(AsignarEtiquetaBody.Valor));
    }
}
