using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoDeHorarioAbiertoFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoDeHorarioAbiertoFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoDeHorarioAbiertoFunction;

public class CrearTurnoDeHorarioAbiertoCommandHandlerTests : CommandHandlerAsyncTest<CrearTurnoDeHorarioAbierto>
{
    private const string Nombre = "Turno Abierto";

    private FakeLectorNombresTurno _lector = new();

    protected override ICommandHandlerAsync<CrearTurnoDeHorarioAbierto> Handler =>
        new CrearTurnoDeHorarioAbiertoCommandHandler(EventStore, _lector);

    private CrearTurnoDeHorarioAbierto Comando(int horas, int minutos = 0) =>
        new(GuidAggregateId, Nombre, new HorasYMinutosSolicitadas(horas, minutos));

    private TurnoCreado TurnoCreadoEsperado(TimeOnly fin, int diaOffsetFin = 0) =>
        TurnoCreado.Crear(GuidAggregateId, Nombre,
            [new DatosFranja(new TimeOnly(4, 0), fin, [], [], null, diaOffsetFin)]);

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_EmiteTurnoCreadoYVentana_CuandoDuracionEsSeisHoras()
    {
        Given();
        await WhenAsync(Comando(6));

        Then(TurnoCreadoEsperado(new TimeOnly(10, 0)),
            VentanaDeEntradaModificada.Crear(GuidAggregateId, HorasYMinutos.Crear(14, 0)));
        And<CatalogoTurnos, string>(c => c.Id, GuidAggregateId.ToString());
        And<CatalogoTurnos, int>(c => c.ObtenerDetalle().AmplitudVentanaEntradaEnMinutos, 14 * 60);
        And<CatalogoTurnos, string>(c => c.ToString(), "04:00-10:00");
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_EmiteVentanaDeDieciseisHoras_CuandoDuracionEsCuatroHoras()
    {
        Given();
        await WhenAsync(Comando(4));

        Then(TurnoCreadoEsperado(new TimeOnly(8, 0)),
            VentanaDeEntradaModificada.Crear(GuidAggregateId, HorasYMinutos.Crear(16, 0)));
        And<CatalogoTurnos, int>(c => c.ObtenerDetalle().AmplitudVentanaEntradaEnMinutos, 16 * 60);
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_RestaMinutosDeLaVentana_CuandoDuracionTieneMinutos()
    {
        Given();
        await WhenAsync(Comando(7, 30));

        Then(TurnoCreadoEsperado(new TimeOnly(11, 30)),
            VentanaDeEntradaModificada.Crear(GuidAggregateId, HorasYMinutos.Crear(12, 30)));
        And<CatalogoTurnos, int>(c => c.ObtenerDetalle().AmplitudVentanaEntradaEnMinutos, 12 * 60 + 30);
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_NoEmiteVentana_CuandoDuracionEsVeinteHoras()
    {
        Given();
        await WhenAsync(Comando(20));

        Then(TurnoCreadoEsperado(new TimeOnly(0, 0), diaOffsetFin: 1));
        And<CatalogoTurnos, int>(c => c.ObtenerDetalle().AmplitudVentanaEntradaEnMinutos, 0);
        And<CatalogoTurnos, string>(c => c.ToString(), "04:00-00:00+1");
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_NoPublicaEventosPrivados_CuandoCreaElTurno()
    {
        Given();
        await WhenAsync(Comando(8));

        Then(TurnoCreadoEsperado(new TimeOnly(12, 0)),
            VentanaDeEntradaModificada.Crear(GuidAggregateId, HorasYMinutos.Crear(12, 0)));
        ThenIsPublishedPrivately();
        And<CatalogoTurnos, string>(c => c.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_LanzaAggregateException_CuandoDuracionEsCero()
    {
        Given();
        var act = async () => await WhenAsync(Comando(0));

        var ex = await act.Should().ThrowExactlyAsync<AggregateException>();
        ex.Which.InnerExceptions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_LanzaAggregateException_CuandoDuracionExcedeVeinteHoras()
    {
        Given();
        var act = async () => await WhenAsync(Comando(20, 1));

        var ex = await act.Should().ThrowExactlyAsync<AggregateException>();
        ex.Which.InnerExceptions.Should().Contain(e =>
            e.Message.Contains(CatalogoTurnos.Mensajes.DuracionHorarioAbiertoFueraDeRango));
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_LanzaAggregateException_CuandoMinutosFueraDeRango()
    {
        Given();
        var act = async () => await WhenAsync(Comando(6, 60));

        await act.Should().ThrowExactlyAsync<AggregateException>();
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_LanzaRecursoYaExisteException_CuandoTurnoYaExiste()
    {
        Given(TurnoCreado.Crear(GuidAggregateId, "Otro nombre", []));

        var act = async () => await WhenAsync(Comando(6));

        await act.Should().ThrowExactlyAsync<RecursoYaExisteException>()
            .WithMessage($"*{CrearTurnoDeHorarioAbiertoCommandHandler.Mensajes.TurnoYaExiste}*");
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_LanzaReglaDeNegocioDeclinada_CuandoNombreEstaDuplicado()
    {
        _lector = new FakeLectorNombresTurno("Turno Abierto");
        Given();

        var act = async () => await WhenAsync(Comando(6));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{CrearTurnoDeHorarioAbiertoCommandHandler.Mensajes.NombreDuplicado}*");
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_LanzaReglaDeNegocioDeclinada_CuandoNombreSoloDifiereEnEspaciosYMayusculas()
    {
        _lector = new FakeLectorNombresTurno("  turno   ABIERTO ");
        Given();

        var act = async () => await WhenAsync(Comando(6));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{CrearTurnoDeHorarioAbiertoCommandHandler.Mensajes.NombreDuplicado}*");
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_LanzaAggregateException_CuandoDuracionEsInvalidaYTurnoYaExiste()
    {
        Given(TurnoCreado.Crear(GuidAggregateId, "Otro nombre", []));

        var act = async () => await WhenAsync(Comando(0));

        await act.Should().ThrowExactlyAsync<AggregateException>();
    }
}
