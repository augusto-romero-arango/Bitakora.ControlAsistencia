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

    [Fact]
    public async Task Validar_Aprueba_CuandoValorNoEsVacio()
    {
        var resultado = await Validar(BodyValido());

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_RechazaValor_CuandoEstaVacio()
    {
        var resultado = await Validar(BodyValido() with { Valor = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(AsignarEtiquetaBody.Valor));
    }

    // NotEmpty tambien rechaza whitespace. Este validator es la unica guarda de forma de Valor: si
    // "   " pasara, el ArgumentException de Etiqueta.Crear no se traduce y saldria 500 en vez de 400.
    [Fact]
    public async Task Validar_RechazaValor_CuandoEsSoloEspacios()
    {
        var resultado = await Validar(BodyValido() with { Valor = "   " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(AsignarEtiquetaBody.Valor));
    }
}
