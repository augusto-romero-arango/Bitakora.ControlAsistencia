using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirNombresFunction;

public class CorregirNombresBodyValidatorTests
{
    private readonly CorregirNombresBodyValidator _validator = new();

    private static CorregirNombresBody BodyValido() => new(
        PrimerNombre: "Luis",
        SegundoNombre: "Augusto",
        PrimerApellido: "Barreto",
        SegundoApellido: null);

    private Task<ValidationResult> Validar(CorregirNombresBody body) =>
        _validator.ValidateAsync(body, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Validar_Aprueba_CuandoTodosLosCamposSonCorrectos()
    {
        var resultado = await Validar(BodyValido());

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_RechazaPrimerNombre_CuandoEstaVacio()
    {
        var resultado = await Validar(BodyValido() with { PrimerNombre = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CorregirNombresBody.PrimerNombre));
    }

    // NotEmpty tambien rechaza whitespace: sin esta guarda, un "   " llegaria a NombreColaborador.Crear,
    // cuyo ArgumentException no se traduce (500 en vez de 400). Igual para PrimerApellido.
    [Fact]
    public async Task Validar_RechazaPrimerNombre_CuandoEsSoloEspacios()
    {
        var resultado = await Validar(BodyValido() with { PrimerNombre = "   " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CorregirNombresBody.PrimerNombre));
    }

    [Fact]
    public async Task Validar_RechazaPrimerApellido_CuandoEstaVacio()
    {
        var resultado = await Validar(BodyValido() with { PrimerApellido = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CorregirNombresBody.PrimerApellido));
    }

    [Fact]
    public async Task Validar_RechazaPrimerApellido_CuandoEsSoloEspacios()
    {
        var resultado = await Validar(BodyValido() with { PrimerApellido = "   " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CorregirNombresBody.PrimerApellido));
    }

    // Opcionales: NombreColaborador.Crear ya los normaliza a ausente.
    [Fact]
    public async Task Validar_Aprueba_CuandoLosSegundosNombresYApellidosSonAusentes()
    {
        var resultado = await Validar(BodyValido() with { SegundoNombre = null, SegundoApellido = null });

        resultado.IsValid.Should().BeTrue();
    }
}
