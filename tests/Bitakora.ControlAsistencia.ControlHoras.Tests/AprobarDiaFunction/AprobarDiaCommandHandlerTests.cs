// Stream ID compuesto (dc:{codigo}:{yyyyMMdd}), no GuidAggregateId: overloads explicitos de Given/Then/And.
// CA-ADR-0030: el aggregate declina con resultado y el handler traduce a ReglaDeNegocioDeclinadaException.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction;
using Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction.CommandHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
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
    private static readonly TimeOnly HoraInicioSegundaFranjaEnConflicto = new(15, 0);

    protected override ICommandHandlerAsync<AprobarDia> Handler =>
        new AprobarDiaCommandHandler(EventStore);

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

    // SEDE-01 programada vs SEDE-02 marcada en la entrada.
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

    private static DepuracionDiaRecibida DepuracionConDosFranjasEnConflicto()
    {
        var entradaManana = new DateTime(2026, 8, 24, 6, 0, 0);
        var salidaManana = new DateTime(2026, 8, 24, 14, 0, 0);
        var entradaTarde = new DateTime(2026, 8, 24, 15, 0, 0);
        var salidaTarde = new DateTime(2026, 8, 24, 19, 0, 0);
        IReadOnlyList<FranjaDepurada> franjas =
        [
            new FranjaDepurada(
                HoraInicioFranjaEnConflicto, new TimeOnly(14, 0), 0, entradaManana, salidaManana, false,
                "SEDE-01", "Sede Principal", "CC-100"),
            new FranjaDepurada(
                HoraInicioSegundaFranjaEnConflicto, new TimeOnly(19, 0), 0, entradaTarde, salidaTarde, false,
                "SEDE-03", "Sede Sur", "CC-300")
        ];
        IReadOnlyList<MarcacionDelDia> marcaciones =
        [
            new MarcacionDelDia(entradaManana, "Entrada", "SEDE-02", "Sede Norte", "CC-200"),
            new MarcacionDelDia(salidaManana, "Salida"),
            new MarcacionDelDia(entradaTarde, "Entrada", "SEDE-04", "Sede Oriente", "CC-400"),
            new MarcacionDelDia(salidaTarde, "Salida")
        ];
        return new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, null, "Jornada partida", franjas, marcaciones,
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []));
    }

    private static DecisionDeSede DecisionValida() =>
        new(HoraInicioFranjaEnConflicto, "SEDE-02");

    [Fact]
    public async Task AprobarDia_EmiteDiaAprobado_CuandoElDiaNoTieneConflictosDeSede()
    {
        Given(StreamId, DepuracionSinConflicto());

        await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, []));

        Then(StreamId, DiaAprobado.Crear(StreamId, CodigoColaborador, Fecha, []));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }

    [Fact]
    public async Task AprobarDia_EmiteDiaAprobadoConLaCandidataCompleta_CuandoLaDecisionEsValida()
    {
        Given(StreamId, DepuracionConFranjaEnConflicto());

        await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, [DecisionValida()]));

        Then(StreamId, DiaAprobado.Crear(StreamId, CodigoColaborador, Fecha,
            [new SedeDecidida(HoraInicioFranjaEnConflicto, "SEDE-02", "Sede Norte", "CC-200")]));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }

    [Fact]
    public async Task AprobarDia_LanzaReglaDeNegocioDeclinadaException_CuandoHayConflictosSinDecidir()
    {
        Given(StreamId, DepuracionConFranjaEnConflicto());

        var act = async () => await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, []));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AprobarDiaCommandHandler.Mensajes.ConflictosSinDecidir}*");
        Then(StreamId);
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    [Fact]
    public async Task AprobarDia_LanzaReglaDeNegocioDeclinadaException_CuandoElPayloadDecideSoloUnaDeLasFranjasEnConflicto()
    {
        Given(StreamId, DepuracionConDosFranjasEnConflicto());

        var act = async () => await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, [DecisionValida()]));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AprobarDiaCommandHandler.Mensajes.ConflictosSinDecidir}*");
        Then(StreamId);
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    [Fact]
    public async Task AprobarDia_LanzaReglaDeNegocioDeclinadaException_CuandoElCodigoSedeNoEsCandidata()
    {
        Given(StreamId, DepuracionConFranjaEnConflicto());

        var act = async () => await WhenAsync(new AprobarDia(
            CodigoColaborador, Fecha, [new DecisionDeSede(HoraInicioFranjaEnConflicto, "SEDE-99")]));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AprobarDiaCommandHandler.Mensajes.CodigoSedeNoCandidata}*");
        Then(StreamId);
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    [Fact]
    public async Task AprobarDia_LanzaReglaDeNegocioDeclinadaException_CuandoLaDecisionEsParaUnaFranjaSinConflicto()
    {
        Given(StreamId, DepuracionSinConflicto());

        var act = async () => await WhenAsync(new AprobarDia(
            CodigoColaborador, Fecha, [new DecisionDeSede(new TimeOnly(6, 0), "SEDE-01")]));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AprobarDiaCommandHandler.Mensajes.DecisionParaFranjaInvalida}*");
        Then(StreamId);
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    [Fact]
    public async Task AprobarDia_LanzaReglaDeNegocioDeclinadaException_CuandoLaHoraInicioProgramadaNoCorrespondeAUnaFranja()
    {
        Given(StreamId, DepuracionConFranjaEnConflicto());

        var act = async () => await WhenAsync(new AprobarDia(
            CodigoColaborador, Fecha, [new DecisionDeSede(new TimeOnly(22, 0), "SEDE-02")]));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AprobarDiaCommandHandler.Mensajes.DecisionParaFranjaInvalida}*");
        Then(StreamId);
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    // Las aprobaciones son definitivas: re-aprobar es error.
    [Fact]
    public async Task AprobarDia_LanzaReglaDeNegocioDeclinadaException_CuandoElDiaYaEstaAprobado()
    {
        Given(StreamId, DepuracionSinConflicto(), DiaAprobado.Crear(StreamId, CodigoColaborador, Fecha, []));

        var act = async () => await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, []));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AprobarDiaCommandHandler.Mensajes.DiaYaAprobado}*");
        Then(StreamId);
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }

    // Aval del vacio: aprobar un dia sin stream atestigua "no vino y no debia venir".
    [Fact]
    public async Task AprobarDia_CreaElStreamConDiaAprobado_CuandoElDiaNoTieneStreamYNoHayDecisiones()
    {
        await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, []));

        Then(StreamId, DiaAprobado.Crear(StreamId, CodigoColaborador, Fecha, []));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }

    // Sin And<>: el aggregate declina antes de StartStream, no hay estado que verificar.
    [Fact]
    public async Task AprobarDia_LanzaReglaDeNegocioDeclinadaException_CuandoElDiaNoTieneStreamYElPayloadTraeDecisiones()
    {
        var act = async () => await WhenAsync(new AprobarDia(
            CodigoColaborador, Fecha, [new DecisionDeSede(new TimeOnly(6, 0), "SEDE-01")]));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AprobarDiaCommandHandler.Mensajes.DecisionParaFranjaInvalida}*");
        Then(StreamId);
    }

    [Fact]
    public async Task AprobarDia_EmiteDiaAprobado_CuandoElDiaEsDeAusenciaConMarcaciones()
    {
        Given(StreamId, new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, null, null, [],
            [new MarcacionDelDia(new DateTime(2026, 8, 24, 6, 0, 0), "Entrada")],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []), "Vacaciones"));

        await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, []));

        Then(StreamId, DiaAprobado.Crear(StreamId, CodigoColaborador, Fecha, []));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }

    [Fact]
    public async Task AprobarDia_EmiteDiaAprobado_CuandoElDiaEsDeAusenciaSinMarcaciones()
    {
        Given(StreamId, new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, null, null, [], [],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []), "Vacaciones"));

        await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, []));

        Then(StreamId, DiaAprobado.Crear(StreamId, CodigoColaborador, Fecha, []));
        And<DiaCalculadoAggregateRoot, string>(
            StreamId, d => d.GenerarDepuracionDelDia().MotivoAusencia!, "Vacaciones");
    }

    // pertenencia null = foto anterior a #1139.
    private static DepuracionDiaRecibida DepuracionConPertenencia(
        string? pertenencia, bool conConflictoDeSede = false)
    {
        var entrada = new DateTime(2026, 8, 24, 6, 0, 0);
        var salida = new DateTime(2026, 8, 24, 14, 0, 0);
        var franja = conConflictoDeSede
            ? new FranjaDepurada(
                HoraInicioFranjaEnConflicto, new TimeOnly(14, 0), 0, entrada, salida, false,
                "SEDE-01", "Sede Principal", "CC-100")
            : new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, entrada, salida, false);
        IReadOnlyList<MarcacionDelDia> marcaciones =
        [
            conConflictoDeSede
                ? new MarcacionDelDia(entrada, "Entrada", "SEDE-02", "Sede Norte", "CC-200", pertenencia)
                : new MarcacionDelDia(entrada, "Entrada", Pertenencia: pertenencia),
            new MarcacionDelDia(salida, "Salida", Pertenencia: pertenencia)
        ];
        return new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, null, "Manana", [franja], marcaciones,
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []));
    }

    [Fact]
    public async Task AprobarDia_LanzaReglaDeNegocioDeclinadaException_CuandoHayMarcacionesAmbiguas()
    {
        Given(StreamId, DepuracionConPertenencia("Ambigua"));

        var act = async () => await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, []));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AprobarDiaCommandHandler.Mensajes.MarcacionesAmbiguasSinResolver}*");
        Then(StreamId);
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    [Fact]
    public async Task AprobarDia_DeclinaConMarcacionesAmbiguas_CuandoTambienHayConflictosDeSedeSinDecidir()
    {
        Given(StreamId, DepuracionConPertenencia("Ambigua", conConflictoDeSede: true));

        var act = async () => await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, []));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AprobarDiaCommandHandler.Mensajes.MarcacionesAmbiguasSinResolver}*");
        Then(StreamId);
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    [Fact]
    public async Task AprobarDia_EmiteDiaAprobado_CuandoLasMarcacionesSonDelTurno()
    {
        Given(StreamId, DepuracionConPertenencia("DelTurno"));

        await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, []));

        Then(StreamId, DiaAprobado.Crear(StreamId, CodigoColaborador, Fecha, []));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }

    [Fact]
    public async Task AprobarDia_EmiteDiaAprobado_CuandoLasMarcacionesSonFueraDeCobertura()
    {
        Given(StreamId, DepuracionConPertenencia("FueraDeCobertura"));

        await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, []));

        Then(StreamId, DiaAprobado.Crear(StreamId, CodigoColaborador, Fecha, []));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }

    // Lectura legada: foto anterior a #1139.
    [Fact]
    public async Task AprobarDia_EmiteDiaAprobado_CuandoLasMarcacionesNoTraenPertenencia()
    {
        Given(StreamId, DepuracionConPertenencia(null));

        await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, []));

        Then(StreamId, DiaAprobado.Crear(StreamId, CodigoColaborador, Fecha, []));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }

    [Fact]
    public async Task AprobarDia_EmiteDiaAprobado_CuandoUnaFotoNuevaYaNoTraeAmbiguas()
    {
        Given(StreamId, DepuracionConPertenencia("Ambigua"), DepuracionConPertenencia("DelTurno"));

        await WhenAsync(new AprobarDia(CodigoColaborador, Fecha, []));

        Then(StreamId, DiaAprobado.Crear(StreamId, CodigoColaborador, Fecha, []));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamId, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }
}
