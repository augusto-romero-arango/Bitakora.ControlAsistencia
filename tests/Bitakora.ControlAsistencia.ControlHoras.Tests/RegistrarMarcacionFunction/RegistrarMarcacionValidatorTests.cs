// El endpoint de RegistrarMarcacion es publico y anonimo (sin APIM ni restriccion de red): estas
// reglas de forma rechazan el request antes del command handler.
//
// IsValid decide el 400 y Errors arma el ValidationProblemDetails: cada rechazo afirma ambos.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.CommandHandler;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RegistrarMarcacionFunction;

public class RegistrarMarcacionValidatorTests
{
    private readonly RegistrarMarcacionValidator _validator = new();

    private static RegistrarMarcacion ComandoValido() =>
        new("EMP-001", new DateTime(2026, 3, 15, 8, 9, 59), "ENTRADA", "DEV-001");

    private Task<ValidationResult> Validar(RegistrarMarcacion comando) =>
        _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Validar_Aprueba_CuandoTodosLosCamposSonCorrectos()
    {
        var resultado = await Validar(ComandoValido());

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoEsNull()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = null! });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarMarcacion.CodigoColaborador));
    }

    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoEstaVacio()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarMarcacion.CodigoColaborador));
    }

    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoSonSoloEspacios()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "   " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarMarcacion.CodigoColaborador));
    }

    // ComputarStreamId usa ':' como separador ("rdm:{codigo}:{timestamp}"): un codigo con ':' podria
    // fabricar el stream ID de otra combinacion legitima. El valor no esta vacio, asi que el unico error
    // posible es el del separador.
    [Fact]
    public async Task Validar_RechazaCodigoColaborador_CuandoContieneDosPuntos()
    {
        var resultado = await Validar(ComandoValido() with { CodigoColaborador = "EMP:001" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarMarcacion.CodigoColaborador));
    }

    [Fact]
    public async Task Validar_RechazaTimestamp_CuandoEsDefault()
    {
        var resultado = await Validar(ComandoValido() with { Timestamp = default });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarMarcacion.Timestamp));
    }

    [Fact]
    public async Task Validar_Aprueba_CuandoTipoMarcacionEsNull()
    {
        var resultado = await Validar(ComandoValido() with { TipoMarcacion = null });

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_Aprueba_CuandoDispositivoIdEsNull()
    {
        var resultado = await Validar(ComandoValido() with { DispositivoId = null });

        resultado.IsValid.Should().BeTrue();
    }
}
