// Issue #467: tests de la reaccion del dueno del dato (MEF-ADR-0046) que resuelve la sede de una
// marcacion. La reaccion no persiste eventos -- consulta el read-side propio (dos lookups) y
// publica; no hay aggregate involucrado (Notas tecnicas del issue). Por eso estos tests NO usan
// Then()/And<>() del harness (que verifican el stream de un AggregateRoot vs EventStore): en su
// lugar, ThenIsPublishedPrivately() verifica la publicacion y las llamadas al fake
// ILectorSedesParaMarcacion + el fake ILogger verifican el resto del comportamiento observable
// (cortocircuito antes de tocar el read model, warning logueado o no).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.PrivateEvents.Sedes;
using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado;
using Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado;

public class RegistroDeMarcacionCreadoEventHandlerTests
    : PrivateEventHandlerAsyncTest<RegistroDeMarcacionCreado>
{
    private const string CodigoColaborador = "EMP-001";
    private const string DispositivoId = "DEV-001";
    private const string SedeId = "s:001"; // stream key completo de la sede (CA-ADR-0031/MEF-ADR-0037)
    private const string CodigoSede = "001";
    private const string NombreSede = "Sede Principal";
    private const string CentroDeCostos = "CC-100";
    private static readonly DateTime TimestampNormalizado = new(2026, 3, 15, 8, 9, 0);

    private FakeLectorSedesParaMarcacion _lector = new();
    private readonly FakeLogger _logger = new();

    protected override IPrivateEventHandlerAsync<RegistroDeMarcacionCreado> Handler =>
        new RegistroDeMarcacionCreadoEventHandler(_lector, PrivateEventSender, _logger);

    private static RegistroDeMarcacionCreado CrearRegistro(string? dispositivoId = DispositivoId) =>
        new(CodigoColaborador, TimestampNormalizado, "ENTRADA", dispositivoId);

    private static UbicacionDispositivo CrearUbicacion() => new(DispositivoId, SedeId);

    private static FichaSede CrearFichaSede(bool activa = true, string? centroDeCostos = CentroDeCostos) =>
        new(SedeId, CodigoSede, NombreSede, "Bogota", "Calle 1", centroDeCostos, activa, [DispositivoId]);

    private static SedeDeMarcacionResuelta CrearEventoEsperado(string? centroDeCostos = CentroDeCostos) =>
        new(CodigoColaborador, TimestampNormalizado, DispositivoId, CodigoSede, NombreSede, centroDeCostos);

    // CA-1: dispositivo ubicado, sede activa con CC -> publica con la terna de correlacion tal cual
    // llego y el estampado vigente de FichaSede, con groupId = CodigoColaborador (fan-in, #463).
    [Fact]
    public async Task ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado_PublicaSedeDeMarcacionResuelta_CuandoDispositivoTieneUbicacionYFichaVigente()
    {
        _lector = new FakeLectorSedesParaMarcacion(ubicacion: CrearUbicacion(), fichaSede: CrearFichaSede());

        await WhenAsync(CrearRegistro());

        ThenIsPublishedPrivately(
            new PublishOptions { GroupId = CodigoColaborador },
            CrearEventoEsperado());
        _logger.WarningLogueado.Should().BeFalse();
    }

    // CA-2: la sede resuelta esta inactiva -> se publica igual (Activa es filtro de asignabilidad
    // para pantallas, no del enriquecimiento -- el estampado registra donde OCURRIO el hecho).
    [Fact]
    public async Task ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado_PublicaSedeDeMarcacionResuelta_CuandoLaSedeResueltaEstaInactiva()
    {
        _lector = new FakeLectorSedesParaMarcacion(
            ubicacion: CrearUbicacion(), fichaSede: CrearFichaSede(activa: false));

        await WhenAsync(CrearRegistro());

        ThenIsPublishedPrivately(
            new PublishOptions { GroupId = CodigoColaborador },
            CrearEventoEsperado());
    }

    // CA-3: la sede resuelta no tiene centro de costos asignado -> se publica con CentroDeCostos null.
    [Fact]
    public async Task ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado_PublicaConCentroDeCostosNulo_CuandoLaSedeResueltaNoTieneCentroDeCostos()
    {
        _lector = new FakeLectorSedesParaMarcacion(
            ubicacion: CrearUbicacion(), fichaSede: CrearFichaSede(centroDeCostos: null));

        await WhenAsync(CrearRegistro());

        ThenIsPublishedPrivately(
            new PublishOptions { GroupId = CodigoColaborador },
            CrearEventoEsperado(centroDeCostos: null));
    }

    // CA-4: DispositivoId null -> silencio total (dato ausente legitimo, no anomalia): no se
    // publica nada, no se loguea warning, y ni siquiera se consulta el read model.
    [Fact]
    public async Task ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado_NoPublicaNiLoguea_CuandoDispositivoIdEsNulo()
    {
        await WhenAsync(CrearRegistro(dispositivoId: null));

        ThenIsPublishedPrivately();
        _logger.WarningLogueado.Should().BeFalse();
        _lector.LlamadasABuscarUbicacion.Should().Be(0);
        _lector.LlamadasABuscarFichaSede.Should().Be(0);
    }

    // CA-5 (rama 1): DispositivoId presente pero sin UbicacionDispositivo -> maestro incompleto:
    // no publica, loguea warning, y nunca llega a buscar la FichaSede (cortocircuito).
    [Fact]
    public async Task ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado_NoPublicaYLogueaWarning_CuandoNoExisteUbicacionDelDispositivo()
    {
        _lector = new FakeLectorSedesParaMarcacion(ubicacion: null);

        await WhenAsync(CrearRegistro());

        ThenIsPublishedPrivately();
        _logger.WarningLogueado.Should().BeTrue();
        _lector.LlamadasABuscarFichaSede.Should().Be(0);
    }

    // CA-5 (rama 2): la ubicacion resuelve a una sede, pero la FichaSede no se encuentra -> maestro
    // incompleto: no publica, loguea warning.
    [Fact]
    public async Task ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado_NoPublicaYLogueaWarning_CuandoFichaSedeNoSeEncuentra()
    {
        _lector = new FakeLectorSedesParaMarcacion(ubicacion: CrearUbicacion(), fichaSede: null);

        await WhenAsync(CrearRegistro());

        ThenIsPublishedPrivately();
        _logger.WarningLogueado.Should().BeTrue();
    }
}

// ---- Fakes manuales - NO NSubstitute ----

internal sealed class FakeLectorSedesParaMarcacion : ILectorSedesParaMarcacion
{
    private readonly UbicacionDispositivo? _ubicacion;
    private readonly FichaSede? _fichaSede;

    public int LlamadasABuscarUbicacion { get; private set; }
    public int LlamadasABuscarFichaSede { get; private set; }

    public FakeLectorSedesParaMarcacion(UbicacionDispositivo? ubicacion = null, FichaSede? fichaSede = null)
    {
        _ubicacion = ubicacion;
        _fichaSede = fichaSede;
    }

    public Task<UbicacionDispositivo?> BuscarUbicacionAsync(string dispositivoId, CancellationToken ct = default)
    {
        LlamadasABuscarUbicacion++;
        return Task.FromResult(_ubicacion);
    }

    public Task<FichaSede?> BuscarFichaSedeAsync(string sedeId, CancellationToken ct = default)
    {
        LlamadasABuscarFichaSede++;
        return Task.FromResult(_fichaSede);
    }
}

internal sealed class FakeLogger : ILogger<RegistroDeMarcacionCreadoEventHandler>
{
    public bool WarningLogueado { get; private set; }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.Warning)
            WarningLogueado = true;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}
