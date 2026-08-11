// Issue #351: validacion de forma del comando CorregirNombres en el borde (CA-5).
// Patron de referencia: TerminarVinculacionValidatorTests/RegistrarColaboradorValidatorTests
// (ValidateAsync + verificacion de PropertyName en resultado.Errors).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction;
using Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction.CommandHandler;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirNombresFunction;

public class CorregirNombresValidatorTests
{
    private readonly CorregirNombresValidator _validator = new();

    private static CorregirNombres ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: "79543210",
        PrimerNombre: "Luis",
        SegundoNombre: "Augusto",
        PrimerApellido: "Barreto",
        SegundoApellido: null);

    private Task<ValidationResult> Validar(CorregirNombres comando) =>
        _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

    // Camino feliz -- todos los campos correctos
    [Fact]
    public async Task Validar_Aprueba_CuandoTodosLosCamposSonCorrectos()
    {
        var resultado = await Validar(ComandoValido());

        resultado.IsValid.Should().BeTrue();
    }

    // CA-5: TipoIdentificacion vacio produce 400
    [Fact]
    public async Task Validar_RechazaTipoIdentificacion_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { TipoIdentificacion = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CorregirNombres.TipoIdentificacion));
    }

    // CA-5: TipoIdentificacion fuera de la lista cerrada (PILA: CC/CE/TI/PA/PT) produce 400, no 500
    [Fact]
    public async Task Validar_RechazaTipoIdentificacion_CuandoNoEstaEnLaListaCerrada()
    {
        var resultado = await Validar(ComandoValido() with { TipoIdentificacion = "XX" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CorregirNombres.TipoIdentificacion));
    }

    // TipoIdentificacion en minusculas ("cc") DEBE seguir siendo valido -- la normalizacion de
    // entrada (trim+MAYUSCULAS antes de TipoIdentificacion.Desde) es responsabilidad del borde,
    // no un rechazo (mismo criterio que TerminarVinculacionValidator/RegistrarColaboradorValidator).
    [Fact]
    public async Task Validar_Aprueba_CuandoTipoIdentificacionLlegaEnMinusculas()
    {
        var resultado = await Validar(ComandoValido() with { TipoIdentificacion = "cc" });

        resultado.IsValid.Should().BeTrue();
    }

    // CA-5: NumeroIdentificacion vacio produce 400
    [Fact]
    public async Task Validar_RechazaNumeroIdentificacion_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { NumeroIdentificacion = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CorregirNombres.NumeroIdentificacion));
    }

    // CA-5: PrimerNombre vacio produce 400 -- minimo colombiano (NombreColaborador.Crear, #348)
    [Fact]
    public async Task Validar_RechazaPrimerNombre_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { PrimerNombre = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CorregirNombres.PrimerNombre));
    }

    // CA-5: PrimerApellido vacio produce 400 -- minimo colombiano (NombreColaborador.Crear, #348)
    [Fact]
    public async Task Validar_RechazaPrimerApellido_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { PrimerApellido = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CorregirNombres.PrimerApellido));
    }

    // SegundoNombre/SegundoApellido son OPCIONALES -- ausentes no rechazan (NombreColaborador.Crear
    // ya los normaliza a ausente).
    [Fact]
    public async Task Validar_Aprueba_CuandoLosSegundosNombresYApellidosSonAusentes()
    {
        var resultado = await Validar(
            ComandoValido() with { SegundoNombre = null, SegundoApellido = null });

        resultado.IsValid.Should().BeTrue();
    }
}
