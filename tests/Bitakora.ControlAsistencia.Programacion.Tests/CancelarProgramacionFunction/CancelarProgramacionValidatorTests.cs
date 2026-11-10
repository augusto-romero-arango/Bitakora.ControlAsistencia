using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CancelarProgramacionFunction;
using Bitakora.ControlAsistencia.Programacion.CancelarProgramacionFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CancelarProgramacionFunction;

public class CancelarProgramacionValidatorTests
{
    private readonly CancelarProgramacionValidator _validator = new();

    private static ColaboradorSolicitado DatosColaboradorValidos() =>
        new("CC-12345678", "E001", "Juan Perez");

    private static CancelarProgramacion ComandoValido() => new(
        Guid.NewGuid(),
        DatosColaboradorValidos(),
        [new DateOnly(2026, 4, 7)]);

    [Fact]
    public async Task DebeSerValido_CuandoTodosLosCamposSonCorrectos()
    {
        var resultado = await _validator.ValidateAsync(
            ComandoValido(), TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task DebeTenerError_CuandoIdEsGuidVacio()
    {
        var comando = ComandoValido() with { Id = Guid.Empty };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CancelarProgramacion.Id));
    }

    [Fact]
    public async Task DebeTenerError_CuandoCodigoColaboradorEstaVacio()
    {
        var datosInvalidos = DatosColaboradorValidos() with { CodigoColaborador = "" };
        var comando = ComandoValido() with { Colaborador = datosInvalidos };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName.Contains(nameof(ColaboradorSolicitado.CodigoColaborador)));
    }

    [Fact]
    public async Task DebeTenerError_CuandoIdentificacionEstaVacia()
    {
        var datosInvalidos = DatosColaboradorValidos() with { Identificacion = "" };
        var comando = ComandoValido() with { Colaborador = datosInvalidos };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName.Contains(nameof(ColaboradorSolicitado.Identificacion)));
    }

    [Fact]
    public async Task DebeTenerError_CuandoNombreCompletoEstaVacio()
    {
        var datosInvalidos = DatosColaboradorValidos() with { NombreCompleto = "   " };
        var comando = ComandoValido() with { Colaborador = datosInvalidos };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName.Contains(nameof(ColaboradorSolicitado.NombreCompleto)));
    }

    // Colaborador null no debe lanzar NullReferenceException (desreferencia encadenada): reporta el
    // error en la propiedad Colaborador.
    [Fact]
    public async Task Validar_TieneErrorEnColaborador_CuandoColaboradorEsNull()
    {
        var comando = ComandoValido() with { Colaborador = null! };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CancelarProgramacion.Colaborador));
    }

    [Fact]
    public async Task DebeTenerError_CuandoFechasEstaVacia()
    {
        var comando = ComandoValido() with { Fechas = [] };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CancelarProgramacion.Fechas));
    }

    // Fechas duplicadas se rechazan en el borde: evitan publicar dos veces el mismo dia al bus.
    [Fact]
    public async Task DebeTenerError_CuandoFechasContieneDuplicados()
    {
        var fecha = new DateOnly(2026, 4, 7);
        var comando = ComandoValido() with { Fechas = [fecha, fecha] };

        var resultado = await _validator.ValidateAsync(
            comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CancelarProgramacion.Fechas));
    }
}
