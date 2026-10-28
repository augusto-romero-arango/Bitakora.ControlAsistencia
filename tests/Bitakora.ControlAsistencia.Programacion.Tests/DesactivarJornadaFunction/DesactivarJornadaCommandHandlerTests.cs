using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DesactivarJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.DesactivarJornadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;
using Cosmos.MultiTenancy;

namespace Bitakora.ControlAsistencia.Programacion.Tests.DesactivarJornadaFunction;

public class DesactivarJornadaCommandHandlerTests : CommandHandlerAsyncTest<DesactivarJornada>
{
    private const string Tenant = "tenant-x";
    private static readonly Guid JornadaX = Guid.Parse("019600a0-0000-7000-8000-000000000870");
    private static readonly Guid JornadaY = Guid.Parse("019600a0-0000-7000-8000-000000000871");
    private static readonly string StreamPreferencias = PreferenciasProgramacion.ComputarStreamId(Tenant);

    private readonly FakeLectorPlantillasTurnos _lector = new();

    protected override ICommandHandlerAsync<DesactivarJornada> Handler =>
        new DesactivarJornadaCommandHandler(EventStore, _lector, new FakeTenantContext());

    private static JornadaCreada Creada(Guid id) => JornadaCreada.Crear(id,
        LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0),
            HorasYMinutos.Crear(0, 0), 1));

    [Fact]
    public async Task DesactivarJornada_EmiteJornadaDesactivada_CuandoNoEstaEnUso()
    {
        Given(JornadaX.ToString(), Creada(JornadaX));
        await WhenAsync(new DesactivarJornada(JornadaX));
        Then(JornadaX.ToString(), new JornadaDesactivada(JornadaX));
        And<Jornada, bool>(JornadaX.ToString(), j => j.Activa, false);
    }

    [Fact]
    public async Task DesactivarJornada_EmiteJornadaDesactivada_CuandoLaPredeterminadaEsOtra()
    {
        Given(JornadaX.ToString(), Creada(JornadaX));
        Given(JornadaY.ToString(), Creada(JornadaY));
        Given(StreamPreferencias, new JornadaPredeterminadaAsignada(JornadaY));
        await WhenAsync(new DesactivarJornada(JornadaX));
        Then(JornadaX.ToString(), new JornadaDesactivada(JornadaX));
        And<Jornada, bool>(JornadaX.ToString(), j => j.Activa, false);
    }

    [Fact]
    public async Task DesactivarJornada_LanzaRecursoNoEncontradoException_CuandoLaJornadaNoExiste()
    {
        var act = async () => await WhenAsync(new DesactivarJornada(JornadaX));
        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{DesactivarJornadaCommandHandler.Mensajes.JornadaNoEncontrada}*");
        Then(JornadaX.ToString());
    }

    [Fact]
    public async Task DesactivarJornada_NoEmiteEventosNiConsultaUsos_CuandoYaEstaInactiva()
    {
        Given(JornadaX.ToString(), Creada(JornadaX), new JornadaDesactivada(JornadaX));
        Given(StreamPreferencias, new JornadaPredeterminadaAsignada(JornadaX));
        _lector.Plantillas = ["p1"];
        await WhenAsync(new DesactivarJornada(JornadaX));
        Then(JornadaX.ToString());
        And<Jornada, bool>(JornadaX.ToString(), j => j.Activa, false);
        _lector.Consultas.Should().Be(0);
    }

    [Fact]
    public async Task DesactivarJornada_LanzaReglaDeNegocioDeclinada_CuandoEsLaPredeterminada()
    {
        Given(JornadaX.ToString(), Creada(JornadaX));
        Given(StreamPreferencias, new JornadaPredeterminadaAsignada(JornadaX));
        var act = async () => await WhenAsync(new DesactivarJornada(JornadaX));
        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{DesactivarJornadaCommandHandler.Mensajes.JornadaPredeterminada}*");
        Then(JornadaX.ToString());
        And<Jornada, bool>(JornadaX.ToString(), j => j.Activa, true);
    }

    [Fact]
    public async Task DesactivarJornada_LanzaReglaDeNegocioDeclinadaConIds_CuandoLaUsanPlantillas()
    {
        Given(JornadaX.ToString(), Creada(JornadaX));
        _lector.Plantillas = ["p1", "p2"];
        var act = async () => await WhenAsync(new DesactivarJornada(JornadaX));
        var excepcion = (await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()).Which;
        excepcion.Message.Should().Contain(DesactivarJornadaCommandHandler.Mensajes.JornadaEnUsoPorPlantillas)
            .And.Contain("p1").And.Contain("p2");
        Then(JornadaX.ToString());
        And<Jornada, bool>(JornadaX.ToString(), j => j.Activa, true);
        _lector.JornadaConsultada.Should().Be(JornadaX);
    }

    [Fact]
    public void Mensajes_TienenTextoEnElResx_CuandoSeConsultanPorResourceManager()
    {
        DesactivarJornadaCommandHandler.Mensajes.JornadaNoEncontrada.Should().NotBeNullOrWhiteSpace();
        DesactivarJornadaCommandHandler.Mensajes.JornadaPredeterminada.Should().NotBeNullOrWhiteSpace();
        DesactivarJornadaCommandHandler.Mensajes.JornadaEnUsoPorPlantillas.Should().NotBeNullOrWhiteSpace();
    }

    private sealed class FakeLectorPlantillasTurnos : ILectorPlantillasTurnosPorJornada
    {
        public IReadOnlyList<string> Plantillas { get; set; } = [];
        public int Consultas { get; private set; }
        public Guid? JornadaConsultada { get; private set; }

        public Task<IReadOnlyList<string>> ObtenerPlantillaIdsAsync(Guid jornadaId, CancellationToken ct)
        {
            Consultas++;
            JornadaConsultada = jornadaId;
            return Task.FromResult(Plantillas);
        }
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        public string TenantId => Tenant;
        public string UserId => "usuario";
        public string OrganizationMembershipId => "membresia";
    }
}
