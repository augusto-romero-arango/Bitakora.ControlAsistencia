using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction;
using FluentValidation.Results;

namespace Bitakora.ControlAsistencia.Sedes.Tests.InstalarDispositivoFunction;

public class InstalarDispositivoBodyValidatorTests
{
    private readonly InstalarDispositivoBodyValidator _validator = new();

    private Task<ValidationResult> Validar(InstalarDispositivoBody body) =>
        _validator.ValidateAsync(body, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Validar_Aprueba_CuandoElDispositivoIdEsValido()
    {
        var resultado = await Validar(new InstalarDispositivoBody("DISP-100"));

        resultado.IsValid.Should().BeTrue();
    }

    // CA-5
    [Fact]
    public async Task Validar_RechazaDispositivoId_CuandoEstaVacio()
    {
        var resultado = await Validar(new InstalarDispositivoBody(""));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(
            e => e.PropertyName == nameof(InstalarDispositivoBody.DispositivoId));
    }

    // CA-5: charset URL-safe (MEF-ADR-0043 seccion 1.3) -- este mismo dato se expone luego como
    // segmento de ruta en el DELETE.
    [Fact]
    public async Task Validar_RechazaDispositivoId_CuandoTieneCaracteresFueraDelCharsetUrlSafe()
    {
        var resultado = await Validar(new InstalarDispositivoBody("disp 100/lector"));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(
            e => e.PropertyName == nameof(InstalarDispositivoBody.DispositivoId));
    }
}
