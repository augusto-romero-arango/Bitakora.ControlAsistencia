// Issue #463: cierre del enriquecimiento coreografiado (MEF-ADR-0046). Sedes resolvio
// dispositivo->sede->CC y lo publico como SedeDeMarcacionResuelta (#467); este handler graba el
// estampado en el ControlDiario (SedeDeMarcacionIdentificada) y re-publica DiaDepurado si hubo
// cambios -- mismo patron crear-o-actualizar / huboCambios que RegistroDeMarcacionCreadoEventHandler
// (AdicionarMarcacionCuandoRegistroDeMarcacionCreado), pero este handler solo ACTUALIZA: nunca crea
// un ControlDiario nuevo (CA-3).

using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.EstamparSedeCuandoSedeDeMarcacionResuelta.EventHandler;
using Bitakora.ControlAsistencia.PrivateEvents.Sedes;
using AwesomeAssertions;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
// Alias de tipo: DiaDepurado/MarcacionDelDia/HorasDiscriminadas existen homonimos en
// ControlHoras.DomainEvents (payload por rol, MEF-ADR-0039 decision #6); este archivo re-publica al
// bus, asi que usa los del bus.
using DiaDepurado = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.DiaDepurado;
using MarcacionDelDia = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.MarcacionDelDia;
using HorasDiscriminadas = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.HorasDiscriminadas;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.EstamparSedeCuandoSedeDeMarcacionResuelta;

public class SedeDeMarcacionResueltaEventHandlerTests
    : PrivateEventHandlerAsyncTest<SedeDeMarcacionResuelta>
{
    private const string CodigoColaborador = "EMP-001";
    private const string DispositivoId = "DEV-001";
    private const string CodigoSede = "001";
    private const string NombreSede = "Sede Principal";
    private const string CentroDeCostos = "CC-100";

    // CA-1: hora fuera de ventana nocturna (>= 04:00) - un solo dia calendario
    private static readonly DateTime TimestampFueraDeVentana = new(2026, 3, 15, 8, 9, 0);

    // CA-2: hora dentro de ventana nocturna (< 04:00) - dia calendario + dia anterior
    private static readonly DateTime TimestampDentroDeVentana = new(2026, 3, 15, 2, 30, 0);

    private static readonly string StreamIdDia15 = $"cd:{CodigoColaborador}:20260315";
    private static readonly string StreamIdDia14 = $"cd:{CodigoColaborador}:20260314";

    protected override IPrivateEventHandlerAsync<SedeDeMarcacionResuelta> Handler =>
        new SedeDeMarcacionResueltaEventHandler(EventStore, PrivateEventSender);

    private static SedeDeMarcacionResuelta CrearSedeDeMarcacionResuelta(
        DateTime timestampNormalizado,
        string? centroDeCostos = CentroDeCostos) =>
        new(CodigoColaborador, timestampNormalizado, DispositivoId, CodigoSede, NombreSede, centroDeCostos);

    private static MarcacionAdicionada CrearMarcacionAdicionada(
        string streamId, DateTime timestampNormalizado, string? dispositivoId = DispositivoId) =>
        new(streamId, CodigoColaborador, timestampNormalizado, "ENTRADA", dispositivoId);

    private static SedeDeMarcacionIdentificada CrearSedeDeMarcacionIdentificada(
        string streamId, DateTime timestampNormalizado, string? centroDeCostos = CentroDeCostos) =>
        new(streamId, timestampNormalizado, DispositivoId, CodigoSede, NombreSede, centroDeCostos);

    // CA-1: la marcacion ya existe en el cd: destino -> se persiste SedeDeMarcacionIdentificada con
    // el estampado tal cual llego y se re-publica DiaDepurado. Escenario sin turno asignado (mismo
    // que RegistroDeMarcacionCreado_DejaControlesDeFranjaVacios_CuandoNoHayTurnoPrevio): mantiene el
    // DiaDepurado esperado armado a mano, sin ejecutar Depurar/Consolidar (regla 20).
    [Fact]
    public async Task SedeDeMarcacionResuelta_EstampaSedeYRepublicaDiaDepurado_CuandoLaMarcacionYaExisteEnElControlDiario()
    {
        Given(StreamIdDia15, CrearMarcacionAdicionada(StreamIdDia15, TimestampFueraDeVentana));

        await WhenAsync(CrearSedeDeMarcacionResuelta(TimestampFueraDeVentana));

        Then(StreamIdDia15, CrearSedeDeMarcacionIdentificada(StreamIdDia15, TimestampFueraDeVentana));
        And<ControlDiarioAggregateRoot, string?>(
            StreamIdDia15,
            c => c.Marcaciones.Single(m => m.TimestampNormalizado == TimestampFueraDeVentana).CodigoSede,
            CodigoSede);

        ThenIsPublishedPrivately(new DiaDepurado(
            CodigoColaborador,
            new DateOnly(2026, 3, 15),
            null,
            null,
            [],
            [new MarcacionDelDia(TimestampFueraDeVentana, "ENTRADA")],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), [])));
    }

    // CA-1: el estampado viaja tal cual llego, incluido el caso sin centro de costos asignado.
    [Fact]
    public async Task SedeDeMarcacionResuelta_EstampaSedeSinCentroDeCostos_CuandoLaSedeNoTieneCentroDeCostosAsignado()
    {
        Given(StreamIdDia15, CrearMarcacionAdicionada(StreamIdDia15, TimestampFueraDeVentana));

        await WhenAsync(CrearSedeDeMarcacionResuelta(TimestampFueraDeVentana, centroDeCostos: null));

        Then(StreamIdDia15,
            CrearSedeDeMarcacionIdentificada(StreamIdDia15, TimestampFueraDeVentana, centroDeCostos: null));
        And<ControlDiarioAggregateRoot, string?>(
            StreamIdDia15,
            c => c.Marcaciones.Single(m => m.TimestampNormalizado == TimestampFueraDeVentana).CentroDeCostos,
            null);
    }

    // CA-2: traslape nocturno heredado -- mismos dias-destino que la marcacion (HoraCorteTraslapeNocturno
    // del handler vecino): [00:00, 04:00) estampa en el cd: del dia calendario Y el del dia anterior.
    [Fact]
    public async Task SedeDeMarcacionResuelta_EstampaEnAmbosControlDiariosYRepublicaAmbosDiaDepurado_CuandoLaMarcacionEstaEnVentanaNocturna()
    {
        Given(StreamIdDia15, CrearMarcacionAdicionada(StreamIdDia15, TimestampDentroDeVentana));
        Given(StreamIdDia14, CrearMarcacionAdicionada(StreamIdDia14, TimestampDentroDeVentana));

        await WhenAsync(CrearSedeDeMarcacionResuelta(TimestampDentroDeVentana));

        Then(StreamIdDia15, CrearSedeDeMarcacionIdentificada(StreamIdDia15, TimestampDentroDeVentana));
        Then(StreamIdDia14, CrearSedeDeMarcacionIdentificada(StreamIdDia14, TimestampDentroDeVentana));

        And<ControlDiarioAggregateRoot, string?>(
            StreamIdDia15,
            c => c.Marcaciones.Single(m => m.TimestampNormalizado == TimestampDentroDeVentana).CodigoSede,
            CodigoSede);
        And<ControlDiarioAggregateRoot, string?>(
            StreamIdDia14,
            c => c.Marcaciones.Single(m => m.TimestampNormalizado == TimestampDentroDeVentana).CodigoSede,
            CodigoSede);

        var marcacionNocturna = new MarcacionDelDia(TimestampDentroDeVentana, "ENTRADA");
        ThenIsPublishedPrivately(
            new DiaDepurado(
                CodigoColaborador, new DateOnly(2026, 3, 15), null, null, [], [marcacionNocturna],
                new HorasDiscriminadas(new Dictionary<string, decimal>(), [])),
            new DiaDepurado(
                CodigoColaborador, new DateOnly(2026, 3, 14), null, null, [], [marcacionNocturna],
                new HorasDiscriminadas(new Dictionary<string, decimal>(), [])));
    }

    // CA-3 (problema de orden, carrera A): el ControlDiario destino no existe todavia -- el handler
    // falla y el retry del Service Bus lo resuelve segundos despues. Sin crear stream vacio.
    [Fact]
    public async Task SedeDeMarcacionResuelta_LanzaInvalidOperationException_CuandoElControlDiarioNoExiste()
    {
        // Sin Given - el stream cd:EMP-001:20260315 no existe
        var act = async () => await WhenAsync(CrearSedeDeMarcacionResuelta(TimestampFueraDeVentana));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{SedeDeMarcacionResueltaEventHandler.Mensajes.ControlDiarioNoEncontrado}*");

        Then(StreamIdDia15);
        ThenIsPublishedPrivately();
    }

    // CA-3 (problema de orden, carrera A): el ControlDiario existe pero la marcacion especifica
    // (TimestampNormalizado+DispositivoId) aun no fue adicionada -- misma carrera, mismo retry.
    [Fact]
    public async Task SedeDeMarcacionResuelta_LanzaInvalidOperationException_CuandoLaMarcacionAunNoFueAdicionada()
    {
        // El ControlDiario existe con otra marcacion, distinto minuto -- la de esta sede no ha llegado
        Given(StreamIdDia15, CrearMarcacionAdicionada(StreamIdDia15, new DateTime(2026, 3, 15, 7, 0, 0)));

        var act = async () => await WhenAsync(CrearSedeDeMarcacionResuelta(TimestampFueraDeVentana));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{SedeDeMarcacionResueltaEventHandler.Mensajes.MarcacionNoEncontrada}*");

        Then(StreamIdDia15);
        ThenIsPublishedPrivately();
    }

    // CA-4: estampado repetido de la misma sede para la misma marcacion -> no-op. El aggregate
    // decide el no-op consultando su propio estado (Tell-don't-Ask, MEF-ADR-0012); el handler no
    // inspecciona Marcaciones para esta decision.
    [Fact]
    public async Task SedeDeMarcacionResuelta_NoEmiteEventoNiRepublica_CuandoLaMismaSedeYaFueEstampada()
    {
        Given(StreamIdDia15,
            CrearMarcacionAdicionada(StreamIdDia15, TimestampFueraDeVentana),
            CrearSedeDeMarcacionIdentificada(StreamIdDia15, TimestampFueraDeVentana));

        await WhenAsync(CrearSedeDeMarcacionResuelta(TimestampFueraDeVentana));

        Then(StreamIdDia15);
        ThenIsPublishedPrivately();
        And<ControlDiarioAggregateRoot, string?>(
            StreamIdDia15,
            c => c.Marcaciones.Single(m => m.TimestampNormalizado == TimestampFueraDeVentana).CodigoSede,
            CodigoSede);
    }
}
