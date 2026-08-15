// Issue #377 (MEF-ADR-0043 paso 2): validacion de forma del body reducido (4 campos del nombre) en
// el borde (CA-4). Reemplaza a CorregirNombresValidatorTests (eliminado junto con
// CorregirNombresValidator, que vivia en CommandHandler/): TipoIdentificacion/NumeroIdentificacion
// ya no llegan en el body -- se validan en el FunctionEndpoint via Identificacion.Parsear (ver
// FunctionEndpointTests). Patron de referencia: AsignarEtiquetaBodyValidatorTests (issue #376).

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

    // Camino feliz -- todos los campos correctos
    [Fact]
    public async Task Validar_Aprueba_CuandoTodosLosCamposSonCorrectos()
    {
        var resultado = await Validar(BodyValido());

        resultado.IsValid.Should().BeTrue();
    }

    // CA-4: PrimerNombre vacio produce 400 -- minimo colombiano (NombreColaborador.Crear, #348)
    [Fact]
    public async Task Validar_RechazaPrimerNombre_CuandoEstaVacio()
    {
        var resultado = await Validar(BodyValido() with { PrimerNombre = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CorregirNombresBody.PrimerNombre));
    }

    // NotEmpty de FluentValidation rechaza tambien whitespace, no solo la cadena vacia -- una de las
    // dos unicas guardas de forma que le quedan al body tras la reduccion de #377 (la otra es
    // PrimerApellido). Se fija con un test en vez de asumirse: sin esta guarda, un PrimerNombre
    // "   " llegaria hasta NombreColaborador.Crear, cuyo ArgumentException nadie traduce (500 en vez
    // de 400, MEF-ADR-0004 capa 1).
    [Fact]
    public async Task Validar_RechazaPrimerNombre_CuandoEsSoloEspacios()
    {
        var resultado = await Validar(BodyValido() with { PrimerNombre = "   " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CorregirNombresBody.PrimerNombre));
    }

    // CA-4: PrimerApellido vacio produce 400 -- minimo colombiano (NombreColaborador.Crear, #348)
    [Fact]
    public async Task Validar_RechazaPrimerApellido_CuandoEstaVacio()
    {
        var resultado = await Validar(BodyValido() with { PrimerApellido = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CorregirNombresBody.PrimerApellido));
    }

    // Mismo racional que PrimerNombre: NotEmpty tambien debe rechazar whitespace puro.
    [Fact]
    public async Task Validar_RechazaPrimerApellido_CuandoEsSoloEspacios()
    {
        var resultado = await Validar(BodyValido() with { PrimerApellido = "   " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CorregirNombresBody.PrimerApellido));
    }

    // SegundoNombre/SegundoApellido son OPCIONALES -- ausentes no rechazan (NombreColaborador.Crear
    // ya los normaliza a ausente).
    [Fact]
    public async Task Validar_Aprueba_CuandoLosSegundosNombresYApellidosSonAusentes()
    {
        var resultado = await Validar(BodyValido() with { SegundoNombre = null, SegundoApellido = null });

        resultado.IsValid.Should().BeTrue();
    }
}
