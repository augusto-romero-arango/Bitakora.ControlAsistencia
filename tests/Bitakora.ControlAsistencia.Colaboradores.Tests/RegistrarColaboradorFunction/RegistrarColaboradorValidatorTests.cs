// CodigoColaborador debe ser URL-safe: solo unreserved de RFC 3986 seccion 2.3 (A-Z a-z 0-9 - . _ ~).
// ":" queda fuera porque es el separador de accion. Se rechaza (400), nunca se limpia: a diferencia
// de Identificacion, el codigo lo asigna la empresa y alterarlo cambiaria un dato ajeno.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RegistrarColaboradorFunction;

public class RegistrarColaboradorValidatorTests
{
    private readonly RegistrarColaboradorValidator _validator = new();

    private static RegistrarColaborador ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: "79543210",
        PrimerNombre: "Luis",
        SegundoNombre: "Augusto",
        PrimerApellido: "Barreto",
        SegundoApellido: "Prieto",
        CodigoColaborador: "COL-001",
        FechaInicio: new DateOnly(2026, 1, 15));

    private Task<ValidationResult> Validar(RegistrarColaborador comando) =>
        _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Validar_Aprueba_CuandoTodosLosCamposSonCorrectos()
    {
        var resultado = await Validar(ComandoValido());

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_RechazaTipoIdentificacion_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { TipoIdentificacion = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.TipoIdentificacion));
    }

    [Fact]
    public async Task Validar_RechazaTipoIdentificacion_CuandoNoEstaEnLaListaCerrada()
    {
        var resultado = await Validar(ComandoValido() with { TipoIdentificacion = "XX" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.TipoIdentificacion));
    }

    // El validator no juzga el formato del codigo de tipo: la normalizacion vive en
    // TipoIdentificacion.Desde.
    [Fact]
    public async Task Validar_Aprueba_CuandoTipoIdentificacionLlegaEnMinusculas()
    {
        var resultado = await Validar(ComandoValido() with { TipoIdentificacion = "cc" });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_RechazaNumeroIdentificacion_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { NumeroIdentificacion = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.NumeroIdentificacion));
    }

    [Fact]
    public async Task Validar_RechazaPrimerNombre_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { PrimerNombre = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.PrimerNombre));
    }

    [Fact]
    public async Task Validar_RechazaPrimerApellido_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { PrimerApellido = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.PrimerApellido));
    }

    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.CodigoColaborador));
    }

    // Requerida, sin default del servidor: el tiempo de los hechos viene del cliente (doctrina bitemporal).
    [Fact]
    public async Task Validar_RechazaFechaInicio_CuandoEsDefault()
    {
        var resultado = await Validar(ComandoValido() with { FechaInicio = default });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.FechaInicio));
    }

    [Fact]
    public async Task Validar_Aprueba_CuandoSegundoNombreEsNull()
    {
        var resultado = await Validar(ComandoValido() with { SegundoNombre = null });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_Aprueba_CuandoSegundoApellidoEsNull()
    {
        var resultado = await Validar(ComandoValido() with { SegundoApellido = null });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoSedeEsNull()
    {
        var resultado = await Validar(ComandoValido() with { CodigoSede = null });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoSedeTieneUnValorPresente()
    {
        var resultado = await Validar(ComandoValido() with { CodigoSede = "BOG" });

        resultado.IsValid.Should().BeTrue();
    }

    // Opcional: ausente es valido, pero presente exige valor.
    [Fact]
    public async Task Validar_RechazaCodigoSede_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { CodigoSede = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.CodigoSede));
    }

    [Fact]
    public async Task Validar_RechazaCodigoSede_CuandoEsSoloEspacios()
    {
        var resultado = await Validar(ComandoValido() with { CodigoSede = "   " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.CodigoSede));
    }

    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoColaboradorTieneCaracteresUnreservedNoAlfanumericos()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "a.b_c~2" });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_Aprueba_CuandoCodigoColaboradorEsAlfanumericoConGuion()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "EMP-001" });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneDosPuntos()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "COL:001" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.CodigoColaborador));
    }

    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneEspacio()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "COL 001" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.CodigoColaborador));
    }

    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneCaracterAcentuado()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "CÓDIGO1" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.CodigoColaborador));
    }

    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneBarra()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "COL/001" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.CodigoColaborador));
    }

    // En .NET "$" tambien hace match antes de un "\n" final (a diferencia de "\z"): un patron anclado
    // con "$" aceptaria "COL-001\n", que rompe la URL y habilita CRLF injection en quien lo reenvie.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoTerminaEnSaltoDeLinea()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "COL-001\n" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarColaborador.CodigoColaborador));
    }
}
