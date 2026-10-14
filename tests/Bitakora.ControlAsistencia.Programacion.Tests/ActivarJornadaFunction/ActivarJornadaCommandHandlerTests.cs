using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ActivarJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.ActivarJornadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ActivarJornadaFunction;

public class ActivarJornadaCommandHandlerTests : CommandHandlerAsyncTest<ActivarJornada>
{
    private static readonly Guid JornadaX = Guid.Parse("019600a0-0000-7000-8000-000000000938");

    protected override ICommandHandlerAsync<ActivarJornada> Handler => new ActivarJornadaCommandHandler(EventStore);

    private static JornadaCreada Creada(Guid id) => JornadaCreada.Crear(id,
        LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0),
            HorasYMinutos.Crear(0, 0), 1));

    [Fact]
    public async Task ActivarJornada_EmiteJornadaActivada_CuandoEstaDesactivada()
    {
        Given(JornadaX.ToString(), Creada(JornadaX), new JornadaDesactivada(JornadaX));
        await WhenAsync(new ActivarJornada(JornadaX));
        Then(JornadaX.ToString(), new JornadaActivada(JornadaX));
        And<Jornada, bool>(JornadaX.ToString(), j => j.Activa, true);
    }

    [Fact]
    public async Task ActivarJornada_NoEmiteEventosNiLanza_CuandoYaEstaActiva()
    {
        Given(JornadaX.ToString(), Creada(JornadaX));
        await WhenAsync(new ActivarJornada(JornadaX));
        Then(JornadaX.ToString());
        And<Jornada, bool>(JornadaX.ToString(), j => j.Activa, true);
    }

    [Fact]
    public async Task ActivarJornada_LanzaRecursoNoEncontradoException_CuandoLaJornadaNoExiste()
    {
        var act = async () => await WhenAsync(new ActivarJornada(JornadaX));
        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{ActivarJornadaCommandHandler.Mensajes.JornadaNoEncontrada}*");
        Then(JornadaX.ToString());
    }

    [Fact]
    public void Mensajes_TienenTextoEnElResx_CuandoSeConsultanPorResourceManager()
    {
        ActivarJornadaCommandHandler.Mensajes.JornadaNoEncontrada.Should().NotBeNullOrWhiteSpace();
    }
}
