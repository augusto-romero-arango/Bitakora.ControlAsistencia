// La reaccion no persiste eventos ni tiene aggregate: consulta el read-side propio y publica. Por
// eso estos tests no usan Then()/And<>() del harness, que reconstruyen un AggregateRoot desde el
// TestStore y aqui lanzarian: el efecto observable se verifica con ThenIsPublishedPrivately() mas
// los fakes manuales (warning logueado o no, y si el segundo lookup llego a ocurrir).

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

    // El groupId es el CodigoColaborador: las resoluciones de un mismo colaborador convergen al
    // mismo stream en el consumidor (fan-in con queue de sesion, MEF-ADR-0026).
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

    // Activa es filtro de asignabilidad para pantallas, no del enriquecimiento: el estampado
    // registra donde OCURRIO el hecho.
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

    // Dato ausente legitimo, no anomalia: ni publicacion, ni warning, ni consulta al read model.
    [Fact]
    public async Task ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado_NoPublicaNiLoguea_CuandoDispositivoIdEsNulo()
    {
        await WhenAsync(CrearRegistro(dispositivoId: null));

        ThenIsPublishedPrivately();
        _logger.WarningLogueado.Should().BeFalse();
        _lector.LlamadasABuscarUbicacion.Should().Be(0);
        _lector.LlamadasABuscarFichaSede.Should().Be(0);
    }

    // Maestro incompleto: warning sin publicacion, y sin llegar a buscar la FichaSede.
    [Fact]
    public async Task ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado_NoPublicaYLogueaWarning_CuandoNoExisteUbicacionDelDispositivo()
    {
        _lector = new FakeLectorSedesParaMarcacion(ubicacion: null);

        await WhenAsync(CrearRegistro());

        ThenIsPublishedPrivately();
        _logger.WarningLogueado.Should().BeTrue();
        _lector.LlamadasABuscarFichaSede.Should().Be(0);
    }

    // El warning sale del .resx (MEF-ADR-0009) y ese recurso solo se resuelve por nombre en
    // runtime: si el .resx dejara de embeberse con el nombre que espera el ResourceManager,
    // GetString devolveria null y el warning saldria vacio -- verde en cualquier test que solo mire
    // el LogLevel. Se afirma el mensaje ya formateado, con los dos valores interpolados.
    [Fact]
    public async Task ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado_LogueaElMensajeDelResxConColaboradorYDispositivo_CuandoNoExisteUbicacionDelDispositivo()
    {
        _lector = new FakeLectorSedesParaMarcacion(ubicacion: null);

        await WhenAsync(CrearRegistro());

        _logger.UltimoWarningFormateado.Should()
            .Be(string.Format(
                RegistroDeMarcacionCreadoEventHandler.Mensajes.DispositivoDesconocidoMarcando
                    .Replace("{CodigoColaborador}", "{0}")
                    .Replace("{DispositivoId}", "{1}"),
                CodigoColaborador,
                DispositivoId));
    }

    // Segunda rama del maestro incompleto: la ubicacion resuelve, pero la sede no tiene ficha.
    [Fact]
    public async Task ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado_NoPublicaYLogueaWarning_CuandoFichaSedeNoSeEncuentra()
    {
        _lector = new FakeLectorSedesParaMarcacion(ubicacion: CrearUbicacion(), fichaSede: null);

        await WhenAsync(CrearRegistro());

        ThenIsPublishedPrivately();
        _logger.WarningLogueado.Should().BeTrue();
    }
}

// ---- Fakes manuales, nunca NSubstitute ----

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

    public string? UltimoWarningFormateado { get; private set; }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel != LogLevel.Warning)
            return;

        WarningLogueado = true;
        UltimoWarningFormateado = formatter(state, exception);
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}
