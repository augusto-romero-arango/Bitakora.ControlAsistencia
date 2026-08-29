// Issue #489: aprobar el dia de un colaborador -- transicion Provisional -> Aprobado. El aggregate
// usa un stream ID compuesto (dc:{codigo}:{yyyyMMdd}), no el GuidAggregateId del harness --
// overloads explicitos de Given/Then/And (regla 18 del test-writer, mismo criterio que
// DiaDepuradoEventHandlerTests). CA-ADR-0030: el aggregate declina con resultado; el handler
// traduce a InvalidOperationException (-> 409, verificada por FunctionEndpoint como 409 Conflict).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction;
using Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction.CommandHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AprobarDiaFunction;

public class AprobarDiaCommandHandlerTests : CommandHandlerAsyncTest<AprobarDia>
{
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateOnly Fecha = new(2026, 8, 24);
    private static readonly string StreamId =
        DiaCalculadoAggregateRoot.ComputarStreamId(CodigoColaborador, Fecha);

    private static readonly TimeOnly HoraInicioFranjaEnConflicto = new(6, 0);

    protected override ICommandHandlerAsync<AprobarDia> Handler =>
        new AprobarDiaCommandHandler(EventStore);

    // ---- Precondicion: dia Provisional SIN conflictos de sede ----
    private static DepuracionDiaRecibida DepuracionSinConflicto()
    {
        var entrada = new DateTime(2026, 8, 24, 6, 0, 0);
        var salida = new DateTime(2026, 8, 24, 14, 0, 0);
        var franja = new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, entrada, salida, false);
        return new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, null, "Manana", [franja],
            [new MarcacionDelDia(entrada, "Entrada"), new MarcacionDelDia(salida, "Salida")],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []));
    }

    // ---- Precondicion: dia Provisional CON una franja en conflicto de sede (SEDE-01 programada
    //      vs SEDE-02 marcada en la entrada) ----
    private static DepuracionDiaRecibida DepuracionConFranjaEnConflicto()
    {
        var entrada = new DateTime(2026, 8, 24, 6, 0, 0);
        var salida = new DateTime(2026, 8, 24, 14, 0, 0);
        var franja = new FranjaDepurada(
            HoraInicioFranjaEnConflicto, new TimeOnly(14, 0), 0, entrada, salida, false,
            "SEDE-01", "Sede Principal", "CC-100");
        IReadOnlyList<MarcacionDelDia> marcaciones =
        [
            new MarcacionDelDia(entrada, "Entrada", "SEDE-02", "Sede Norte", "CC-200"),
            new MarcacionDelDia(salida, "Salida")
        ];
        return new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, null, "Manana", [franja], marcaciones,
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []));
    }

    private static AprobarDia.DecisionDeSede DecisionValida() =>
        new(HoraInicioFranjaEnConflicto, "SEDE-02");

    // CA-1: dia Provisional sin conflictos, sin decisiones -> DiaAprobado con SedesDecididas vacia.
    [Fact]
    public async Task AprobarDia_EmiteDiaAprobado_CuandoElDiaNoTieneConflictosDeSede()
    {
        Given(StreamId, DepuracionSinConflicto());

        await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, []));

        Then(StreamId, new DiaAprobado(StreamId, CodigoColaborador, Fecha, []));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }

    // CA-2: franja en conflicto con decision valida -> DiaAprobado carga la candidata completa
    // (codigo + nombre + CC del estampado de su fuente, aqui la marcacion SEDE-02).
    [Fact]
    public async Task AprobarDia_EmiteDiaAprobadoConLaCandidataCompleta_CuandoLaDecisionEsValida()
    {
        Given(StreamId, DepuracionConFranjaEnConflicto());

        await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, [DecisionValida()]));

        Then(StreamId, new DiaAprobado(StreamId, CodigoColaborador, Fecha,
            [new SedeDecidida(HoraInicioFranjaEnConflicto, "SEDE-02", "Sede Norte", "CC-200")]));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }

    // CA-3: conflicto de sede sin decidir (payload vacio) -> 409, sin evento persistido, estado
    // sin cambios.
    [Fact]
    public async Task AprobarDia_LanzaInvalidOperationException_CuandoHayConflictosSinDecidir()
    {
        Given(StreamId, DepuracionConFranjaEnConflicto());

        var act = async () => await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, []));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AprobarDiaCommandHandler.Mensajes.ConflictosSinDecidir}*");
        Then(StreamId);
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    // CA-4: CodigoSede que no es candidata de esa franja -> 409, sin evento persistido.
    [Fact]
    public async Task AprobarDia_LanzaInvalidOperationException_CuandoElCodigoSedeNoEsCandidata()
    {
        Given(StreamId, DepuracionConFranjaEnConflicto());

        var act = async () => await WhenAsync(new AprobarDia(
            CodigoColaborador, Fecha, [new AprobarDia.DecisionDeSede(HoraInicioFranjaEnConflicto, "SEDE-99")]));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AprobarDiaCommandHandler.Mensajes.CodigoSedeNoCandidata}*");
        Then(StreamId);
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    // CA-5, primera variante: decision para una franja SIN conflicto -> 409 (el payload afirma
    // algo que el expediente contradice).
    [Fact]
    public async Task AprobarDia_LanzaInvalidOperationException_CuandoLaDecisionEsParaUnaFranjaSinConflicto()
    {
        Given(StreamId, DepuracionSinConflicto());

        var act = async () => await WhenAsync(new AprobarDia(
            CodigoColaborador, Fecha, [new AprobarDia.DecisionDeSede(new TimeOnly(6, 0), "SEDE-01")]));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AprobarDiaCommandHandler.Mensajes.DecisionParaFranjaInvalida}*");
        Then(StreamId);
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    // CA-5, segunda variante: HoraInicioProgramada que no corresponde a NINGUNA franja del dia ->
    // 409.
    [Fact]
    public async Task AprobarDia_LanzaInvalidOperationException_CuandoLaHoraInicioProgramadaNoCorrespondeAUnaFranja()
    {
        Given(StreamId, DepuracionConFranjaEnConflicto());

        var act = async () => await WhenAsync(new AprobarDia(
            CodigoColaborador, Fecha, [new AprobarDia.DecisionDeSede(new TimeOnly(22, 0), "SEDE-02")]));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AprobarDiaCommandHandler.Mensajes.DecisionParaFranjaInvalida}*");
        Then(StreamId);
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    // CA-6: dia ya Aprobado -> 409, re-aprobar es error (las aprobaciones son definitivas), sin
    // evento nuevo, el estado se conserva Aprobado.
    [Fact]
    public async Task AprobarDia_LanzaInvalidOperationException_CuandoElDiaYaEstaAprobado()
    {
        Given(StreamId, DepuracionSinConflicto(), new DiaAprobado(StreamId, CodigoColaborador, Fecha, []));

        var act = async () => await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, []));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AprobarDiaCommandHandler.Mensajes.DiaYaAprobado}*");
        Then(StreamId);
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }

    // CA-7 (aval del vacio, decision 2026-08-17): aprobar un dia SIN stream es valido -- atestigua
    // "no vino y no debia venir". Crea el stream con DiaAprobado como primer evento.
    [Fact]
    public async Task AprobarDia_CreaElStreamConDiaAprobado_CuandoElDiaNoTieneStreamYNoHayDecisiones()
    {
        // Sin Given - el stream dc:EMP-001:20260824 no existe

        await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, []));

        Then(StreamId, new DiaAprobado(StreamId, CodigoColaborador, Fecha, []));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }

    // CA-7, contracara: dia sin stream + payload con decisiones -> 409 (caso CA-5, el expediente
    // vacio no tiene ninguna franja que decidir). Sin And<>: el aggregate nunca llega a crearse
    // (declina antes de StartStream) -- mismo criterio que
    // TerminarVinculacion_LanzaKeyNotFoundException_CuandoColaboradorNoExiste.
    [Fact]
    public async Task AprobarDia_LanzaInvalidOperationException_CuandoElDiaNoTieneStreamYElPayloadTraeDecisiones()
    {
        // Sin Given - el stream dc:EMP-001:20260824 no existe

        var act = async () => await WhenAsync(new AprobarDia(
            CodigoColaborador, Fecha, [new AprobarDia.DecisionDeSede(new TimeOnly(6, 0), "SEDE-01")]));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{AprobarDiaCommandHandler.Mensajes.DecisionParaFranjaInvalida}*");
        Then(StreamId);
    }
}
