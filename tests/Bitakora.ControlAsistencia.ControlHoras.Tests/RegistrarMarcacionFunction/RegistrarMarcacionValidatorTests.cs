// Issue #279: Validar el comando RegistrarMarcacion en el borde
// RegistrarMarcacion es el unico comando HTTP del repo sin validator y su endpoint es
// publico y anonimo (sin APIM, sin restriccion de red). Este archivo cubre las reglas de
// forma que deben rechazar un request antes de que llegue al command handler.
//
// Patron de referencia: SolicitarProgramacionTurnoValidatorTests (mismo estilo de
// ValidateAsync + verificacion de PropertyName en resultado.Errors).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RegistrarMarcacionFunction;

public class RegistrarMarcacionValidatorTests
{
    private readonly RegistrarMarcacionValidator _validator = new();

    private static RegistrarMarcacion ComandoValido() =>
        new("EMP-001", new DateTime(2026, 3, 15, 8, 9, 59), "ENTRADA", "DEV-001");

    // Camino feliz - todos los campos correctos
    [Fact]
    public async Task DebeSerValido_CuandoTodosLosCamposSonCorrectos()
    {
        var resultado = await _validator.ValidateAsync(
            ComandoValido(), TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeTrue();
    }

    // CA-2: EmpleadoId nulo produce 400
    [Fact]
    public async Task DebeTenerError_CuandoEmpleadoIdEsNull()
    {
        var comando = ComandoValido() with { EmpleadoId = null! };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarMarcacion.EmpleadoId));
    }

    // CA-2: EmpleadoId vacio produce 400
    [Fact]
    public async Task DebeTenerError_CuandoEmpleadoIdEstaVacio()
    {
        var comando = ComandoValido() with { EmpleadoId = "" };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarMarcacion.EmpleadoId));
    }

    // CA-2: EmpleadoId con solo espacios en blanco produce 400
    [Fact]
    public async Task DebeTenerError_CuandoEmpleadoIdSonSoloEspacios()
    {
        var comando = ComandoValido() with { EmpleadoId = "   " };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarMarcacion.EmpleadoId));
    }

    // CA-3: EmpleadoId que contiene ':' produce 400 - cierra la colision de stream ID
    // descrita en el Contexto del issue (ComputarStreamId usa ':' como separador entre
    // EmpleadoId y Timestamp; un EmpleadoId con ':' puede fabricar el mismo stream ID que
    // otra combinacion legitima).
    [Fact]
    public async Task DebeTenerError_CuandoEmpleadoIdContieneDosPuntos()
    {
        var comando = ComandoValido() with { EmpleadoId = "EMP:001" };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarMarcacion.EmpleadoId));
    }

    // CA-4: Timestamp con el valor default de DateTime produce 400
    [Fact]
    public async Task DebeTenerError_CuandoTimestampEsDefault()
    {
        var comando = ComandoValido() with { Timestamp = default };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegistrarMarcacion.Timestamp));
    }

    // Regresion de #105 CA-3: TipoMarcacion sigue siendo opcional - null no debe generar error
    [Fact]
    public async Task DebeSerValido_CuandoTipoMarcacionEsNull()
    {
        var comando = ComandoValido() with { TipoMarcacion = null };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeTrue();
    }

    // Regresion de #105 CA-3: DispositivoId sigue siendo opcional - null no debe generar error
    [Fact]
    public async Task DebeSerValido_CuandoDispositivoIdEsNull()
    {
        var comando = ComandoValido() with { DispositivoId = null };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeTrue();
    }
}
