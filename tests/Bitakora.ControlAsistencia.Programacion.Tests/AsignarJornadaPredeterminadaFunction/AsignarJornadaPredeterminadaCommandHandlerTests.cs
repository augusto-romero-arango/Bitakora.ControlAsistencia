using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarJornadaPredeterminadaFunction;
using Bitakora.ControlAsistencia.Programacion.AsignarJornadaPredeterminadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;
using Cosmos.MultiTenancy;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarJornadaPredeterminadaFunction;

public class AsignarJornadaPredeterminadaCommandHandlerTests : CommandHandlerAsyncTest<AsignarJornadaPredeterminada>
{
    private const string Tenant = "tenant-x";
    private static readonly Guid JornadaX = Guid.Parse("019600a0-0000-7000-8000-000000000858");
    private static readonly Guid JornadaY = Guid.Parse("019600a0-0000-7000-8000-000000000859");
    private static readonly string StreamPreferencias = PreferenciasProgramacion.ComputarStreamId(Tenant);

    private readonly FakeAsegurador _asegurador = new(JornadaX);

    protected override ICommandHandlerAsync<AsignarJornadaPredeterminada> Handler =>
        new AsignarJornadaPredeterminadaCommandHandler(EventStore, _asegurador, new FakeTenantContext());

    private static JornadaCreada Creada(Guid id) => JornadaCreada.Crear(id,
        LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0),
            HorasYMinutos.Crear(0, 0), 1));

    private void DadasDosJornadasYPredeterminadaX()
    {
        Given(JornadaX.ToString(), Creada(JornadaX));
        Given(JornadaY.ToString(), Creada(JornadaY));
        Given(StreamPreferencias, new JornadaPredeterminadaAsignada(JornadaX));
    }

    [Fact]
    public async Task AsignarJornadaPredeterminada_EmiteJornadaPredeterminadaAsignada_CuandoLaJornadaEsOtra()
    {
        DadasDosJornadasYPredeterminadaX();
        await WhenAsync(new AsignarJornadaPredeterminada(JornadaY));
        Then(StreamPreferencias, new JornadaPredeterminadaAsignada(JornadaY));
        And<PreferenciasProgramacion, Guid>(StreamPreferencias, p => p.JornadaPredeterminada(), JornadaY);
        _asegurador.Invocaciones.Should().Be(1);
    }

    [Fact]
    public async Task AsignarJornadaPredeterminada_NoEmiteEventos_CuandoYaEsLaPredeterminada()
    {
        DadasDosJornadasYPredeterminadaX();
        await WhenAsync(new AsignarJornadaPredeterminada(JornadaX));
        Then(StreamPreferencias);
        And<PreferenciasProgramacion, Guid>(StreamPreferencias, p => p.JornadaPredeterminada(), JornadaX);
    }

    [Fact]
    public async Task AsignarJornadaPredeterminada_LanzaRecursoNoEncontradoException_CuandoLaJornadaNoExiste()
    {
        Given(StreamPreferencias, new JornadaPredeterminadaAsignada(JornadaX));
        var act = async () => await WhenAsync(new AsignarJornadaPredeterminada(JornadaY));
        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{AsignarJornadaPredeterminadaCommandHandler.Mensajes.JornadaNoEncontrada}*");
        Then(StreamPreferencias);
        And<PreferenciasProgramacion, Guid>(StreamPreferencias, p => p.JornadaPredeterminada(), JornadaX);
        _asegurador.Invocaciones.Should().Be(1);
    }

    private sealed class FakeAsegurador(Guid jornadaId) : IAseguradorJornadaPredeterminada
    {
        public int Invocaciones { get; private set; }

        public Task<Guid> AsegurarAsync(CancellationToken ct)
        {
            Invocaciones++;
            return Task.FromResult(jornadaId);
        }
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        public string TenantId => Tenant;
        public string UserId => "usuario";
        public string OrganizationMembershipId => "membresia";
    }
}
