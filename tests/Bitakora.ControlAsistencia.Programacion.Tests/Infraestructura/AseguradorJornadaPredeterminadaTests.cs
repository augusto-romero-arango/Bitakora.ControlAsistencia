using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.MultiTenancy;
using Marten.Exceptions;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Infraestructura;

public class AseguradorJornadaPredeterminadaTests
{
    private const string Tenant = "tenant-x";
    private static readonly Guid Existente = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");
    private static readonly Guid Ganadora = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5c");

    private sealed class FakeTenantContext : ITenantContext
    {
        public string TenantId => Tenant;
        public string UserId => "usuario";
        public string OrganizationMembershipId => "membresia";
    }

    private sealed class FakeAlmacen(Guid? predeterminadaInicial = null, Guid? ganadoraEnCarrera = null)
        : IAlmacenPreferenciasProgramacion
    {
        private Guid? _predeterminada = predeterminadaInicial;

        public List<(string Tenant, JornadaCreada Jornada, JornadaPredeterminadaAsignada Asignacion)> Iniciados { get; } = [];
        public int Lecturas { get; private set; }

        public Task<PreferenciasProgramacion?> LeerAsync(string tenantId, CancellationToken ct)
        {
            Lecturas++;
            return Task.FromResult(_predeterminada is { } id
                ? PreferenciasProgramacion.Iniciar(new JornadaPredeterminadaAsignada(id))
                : null);
        }

        public Task IniciarAsync(string tenantId, JornadaCreada jornada, JornadaPredeterminadaAsignada asignacion,
            CancellationToken ct)
        {
            if (ganadoraEnCarrera is { } ganadora)
            {
                _predeterminada = ganadora;
                throw (ExistingStreamIdCollisionException)RuntimeHelpers
                    .GetUninitializedObject(typeof(ExistingStreamIdCollisionException));
            }

            Iniciados.Add((tenantId, jornada, asignacion));
            _predeterminada = asignacion.JornadaId;
            return Task.CompletedTask;
        }
    }

    private static AseguradorJornadaPredeterminada Crear(FakeAlmacen almacen) => new(almacen, new FakeTenantContext());

    [Fact]
    public async Task Asegurar_MaterializaJornadaYPreferencias_CuandoNoExistenPreferencias()
    {
        var almacen = new FakeAlmacen();

        var id = await Crear(almacen).AsegurarAsync(CancellationToken.None);

        id.Should().NotBe(Guid.Empty);
        var iniciado = almacen.Iniciados.Should().ContainSingle().Subject;
        iniciado.Tenant.Should().Be(Tenant);
        iniciado.Asignacion.JornadaId.Should().Be(id);
        iniciado.Jornada.JornadaId.Should().Be(id);
        var esperado = LimitesJornada.Crear(
            HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0), HorasYMinutos.Crear(0, 0), 1);
        iniciado.Jornada.Limites.Should().Be(esperado);
    }

    [Fact]
    public async Task Asegurar_DevuelveLaPredeterminadaSinEscribir_CuandoYaExistenPreferencias()
    {
        var almacen = new FakeAlmacen(predeterminadaInicial: Existente);

        var id = await Crear(almacen).AsegurarAsync(CancellationToken.None);

        id.Should().Be(Existente);
        almacen.Iniciados.Should().BeEmpty();
    }

    [Fact]
    public async Task Asegurar_DevuelveLaMismaJornadaYCreaUnaSola_CuandoSeLlamaDosVeces()
    {
        var almacen = new FakeAlmacen();
        var asegurador = Crear(almacen);

        var primera = await asegurador.AsegurarAsync(CancellationToken.None);
        var segunda = await asegurador.AsegurarAsync(CancellationToken.None);

        segunda.Should().Be(primera);
        almacen.Iniciados.Should().ContainSingle();
    }

    [Fact]
    public async Task Asegurar_ReleePreferenciasYDevuelveLaGanadora_CuandoOtraLlamadaSeAdelanto()
    {
        var almacen = new FakeAlmacen(ganadoraEnCarrera: Ganadora);

        var id = await Crear(almacen).AsegurarAsync(CancellationToken.None);

        id.Should().Be(Ganadora);
        almacen.Iniciados.Should().BeEmpty();
        almacen.Lecturas.Should().Be(2);
    }
}
