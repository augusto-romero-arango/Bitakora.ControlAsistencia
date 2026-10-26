using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoDeHorarioAbiertoFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoDeHorarioAbiertoFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoDeHorarioAbiertoFunction;

public class CrearTurnoDeHorarioAbiertoValidatorTests
{
    private readonly CrearTurnoDeHorarioAbiertoValidator _validator = new();

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_EsValido_CuandoDatosCompletos()
    {
        var comando = new CrearTurnoDeHorarioAbierto(Guid.NewGuid(), "Turno Abierto", new HorasYMinutosSolicitadas(6, 0));

        var resultado = await _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_Rechaza_CuandoTurnoIdEsGuidVacio()
    {
        var comando = new CrearTurnoDeHorarioAbierto(Guid.Empty, "Turno Abierto", new HorasYMinutosSolicitadas(6, 0));

        var resultado = await _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CrearTurnoDeHorarioAbierto.TurnoId));
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_Rechaza_CuandoNombreEstaVacio()
    {
        var comando = new CrearTurnoDeHorarioAbierto(Guid.NewGuid(), "", new HorasYMinutosSolicitadas(6, 0));

        var resultado = await _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CrearTurnoDeHorarioAbierto.Nombre));
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_Rechaza_CuandoDuracionEstaAusente()
    {
        var comando = new CrearTurnoDeHorarioAbierto(Guid.NewGuid(), "Turno Abierto", null!);

        var resultado = await _validator.ValidateAsync(comando, TestContext.Current.CancellationToken);

        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CrearTurnoDeHorarioAbierto.Duracion));
    }
}
