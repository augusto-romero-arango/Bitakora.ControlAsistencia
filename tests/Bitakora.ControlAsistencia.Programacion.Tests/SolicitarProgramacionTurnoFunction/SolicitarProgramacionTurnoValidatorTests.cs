// HU-10: Solicitar programacion de turno del catalogo - tests del validator

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ValueObjects;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SolicitarProgramacionTurnoFunction;

public class SolicitarProgramacionTurnoValidatorTests
{
    private readonly SolicitarProgramacionTurnoValidator _validator = new();

    private static InformacionEmpleado DatosEmpleadoValidos() =>
        new("E001", "CC", "12345678", "Juan", "Perez");

    private static SolicitarProgramacionTurno ComandoValido() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        DatosEmpleadoValidos(),
        [new DateOnly(2026, 4, 7)]);

    // Camino feliz - todos los campos correctos
    [Fact]
    public async Task DebeSerValido_CuandoTodosLosCamposSonCorrectos()
    {
        var resultado = await _validator.ValidateAsync(
            ComandoValido(), TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeTrue();
    }

    // CA-1: Id no puede ser Guid vacio
    [Fact]
    public async Task DebeTenerError_CuandoIdEsGuidVacio()
    {
        var comando = ComandoValido() with { Id = Guid.Empty };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(SolicitarProgramacionTurno.Id));
    }

    // CA-2: TurnoId no puede ser Guid vacio
    [Fact]
    public async Task DebeTenerError_CuandoTurnoIdEsGuidVacio()
    {
        var comando = ComandoValido() with { TurnoId = Guid.Empty };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(SolicitarProgramacionTurno.TurnoId));
    }

    // CA-3: EmpleadoId no puede estar vacio
    [Fact]
    public async Task DebeTenerError_CuandoEmpleadoIdEstaVacio()
    {
        var datosInvalidos = DatosEmpleadoValidos() with { EmpleadoId = "" };
        var comando = ComandoValido() with { Empleado = datosInvalidos };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName.Contains(nameof(InformacionEmpleado.EmpleadoId)));
    }

    // CA-3: TipoIdentificacion no puede estar vacio
    [Fact]
    public async Task DebeTenerError_CuandoTipoIdentificacionEstaVacio()
    {
        var datosInvalidos = DatosEmpleadoValidos() with { TipoIdentificacion = "" };
        var comando = ComandoValido() with { Empleado = datosInvalidos };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName.Contains(nameof(InformacionEmpleado.TipoIdentificacion)));
    }

    // CA-3: NumeroIdentificacion no puede estar vacio
    [Fact]
    public async Task DebeTenerError_CuandoNumeroIdentificacionEstaVacio()
    {
        var datosInvalidos = DatosEmpleadoValidos() with { NumeroIdentificacion = " " };
        var comando = ComandoValido() with { Empleado = datosInvalidos };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName.Contains(nameof(InformacionEmpleado.NumeroIdentificacion)));
    }

    // CA-3: Nombres no puede estar vacio
    [Fact]
    public async Task DebeTenerError_CuandoNombresEstanVacios()
    {
        var datosInvalidos = DatosEmpleadoValidos() with { Nombres = "" };
        var comando = ComandoValido() with { Empleado = datosInvalidos };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName.Contains(nameof(InformacionEmpleado.Nombres)));
    }

    // CA-3: Apellidos no puede estar vacio
    [Fact]
    public async Task DebeTenerError_CuandoApellidosEstanVacios()
    {
        var datosInvalidos = DatosEmpleadoValidos() with { Apellidos = "   " };
        var comando = ComandoValido() with { Empleado = datosInvalidos };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName.Contains(nameof(InformacionEmpleado.Apellidos)));
    }

    // CA-4: Fechas debe tener al menos un elemento
    [Fact]
    public async Task DebeTenerError_CuandoFechasEstaVacia()
    {
        var comando = ComandoValido() with { Fechas = [] };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(SolicitarProgramacionTurno.Fechas));
    }
}
