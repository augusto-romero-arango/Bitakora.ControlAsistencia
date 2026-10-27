using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.ControlHoras.ResolverAmbiguedadDeMarcacionesFunction;
using Bitakora.ControlAsistencia.ControlHoras.ResolverAmbiguedadDeMarcacionesFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;
using DiaDepurado = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.DiaDepurado;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ResolverAmbiguedadDeMarcacionesFunction;

public class ResolverAmbiguedadDeMarcacionesCommandHandlerTests : CommandHandlerAsyncTest<ResolverAmbiguedadDeMarcaciones>
{
    private const string CodigoColaborador = "EMP-001";
    private const string Dispositivo = "DEV-001";

    private static readonly DateOnly Dia20 = new(2026, 10, 20);
    private static readonly DateOnly Dia21 = new(2026, 10, 21);

    private static readonly string StreamV = $"cd:{CodigoColaborador}:20261020";
    private static readonly string StreamD = $"cd:{CodigoColaborador}:20261021";
    private static readonly string StreamDcV = $"dc:{CodigoColaborador}:20261020";
    private static readonly string StreamDcD = $"dc:{CodigoColaborador}:20261021";

    protected override ICommandHandlerAsync<ResolverAmbiguedadDeMarcaciones> Handler =>
        new ResolverAmbiguedadDeMarcacionesCommandHandler(EventStore, PrivateEventSender);

    private static DateTime V(int hora, int minuto) => Dia20.ToDateTime(new TimeOnly(hora, minuto));
    private static DateTime D(int hora, int minuto) => Dia21.ToDateTime(new TimeOnly(hora, minuto));

    private static TurnoDiario Turno(TimeOnly inicio, TimeOnly fin, int offsetFin) =>
        new("Turno", [new FranjaProgramada(inicio, fin, offsetFin, [], [], string.Empty)], string.Empty, 0);

    private static TurnoDiarioAsignado TurnoAsignado(string streamId, DateOnly fecha, TurnoDiario turno) =>
        new(streamId, new ColaboradorProgramado("1098765432", CodigoColaborador, "Ana Perez"),
            fecha, turno, Guid.NewGuid());

    private static MarcacionAdicionada Marcacion(string streamId, DateTime timestamp) =>
        new(streamId, CodigoColaborador, timestamp, "ENTRADA", Dispositivo);

    private static DecisionDeMarcacion Decision(DateTime marcacion, DateOnly fecha) => new(marcacion, fecha);

    private static ResolverAmbiguedadDeMarcaciones Comando(params DecisionDeMarcacion[] decisiones) =>
        new(CodigoColaborador, Dia21, decisiones);

    private static string PertenenciasPorHora(DiaDepurado dia) =>
        string.Join(" ", dia.Marcaciones.Select(m => $"{m.Timestamp:HH:mm}={m.Pertenencia}"));

    private IEnumerable<DateOnly> FechasPublicadas()
    {
        PrivateEventSender.Events.Should().BeEmpty();
        PrivateEventSender.PublishedWithOptions.Should().OnlyContain(p => p.Options.GroupId == CodigoColaborador);
        return PrivateEventSender.PublishedWithOptions
            .SelectMany(p => p.Events).OfType<DiaDepurado>().Select(d => d.Fecha).Order();
    }

    // Vispera 17:00-01:00 (cobertura hasta 05:00, entrada 16:55) y dia 06:00-14:00 (cobertura desde
    // 04:00, marcaciones 06:20 y 14:05); la 04:30 cae en ambas coberturas.
    // El harness admite un solo Given por stream: una resolucion previa viaja en el mismo Given.
    private void GivenSolapeDeLas0430(MarcacionDecidida? resueltaAntes = null)
    {
        object[] vispera =
        [
            TurnoAsignado(StreamV, Dia20, Turno(new TimeOnly(17, 0), new TimeOnly(1, 0), 1)),
            Marcacion(StreamV, V(16, 55)),
            Marcacion(StreamV, D(4, 30)),
            CoberturaVecinaActualizada.Crear(StreamV, DiaVecino.DiaSiguiente, D(4, 0))
        ];
        object[] dia =
        [
            TurnoAsignado(StreamD, Dia21, Turno(new TimeOnly(6, 0), new TimeOnly(14, 0), 0)),
            Marcacion(StreamD, D(4, 30)),
            Marcacion(StreamD, D(6, 20)),
            Marcacion(StreamD, D(14, 5)),
            CoberturaVecinaActualizada.Crear(StreamD, DiaVecino.Vispera, D(5, 0))
        ];
        if (resueltaAntes is null)
        {
            Given(StreamV, vispera);
            Given(StreamD, dia);
        }
        else
        {
            Given(StreamV, [.. vispera, ResolucionDeV(resueltaAntes)]);
            Given(StreamD, [.. dia, ResolucionDeD(resueltaAntes)]);
        }
    }

    // Vispera 17:00-01:00 y dia 05:30-13:30 (cobertura desde 03:30): 04:00 y 04:50 caen en ambas.
    private void GivenSolapeDeDosMarcaciones()
    {
        Given(StreamV,
            TurnoAsignado(StreamV, Dia20, Turno(new TimeOnly(17, 0), new TimeOnly(1, 0), 1)),
            Marcacion(StreamV, V(16, 55)),
            Marcacion(StreamV, D(4, 0)),
            Marcacion(StreamV, D(4, 50)),
            CoberturaVecinaActualizada.Crear(StreamV, DiaVecino.DiaSiguiente, D(3, 30)));
        Given(StreamD,
            TurnoAsignado(StreamD, Dia21, Turno(new TimeOnly(5, 30), new TimeOnly(13, 30), 0)),
            Marcacion(StreamD, D(4, 0)),
            Marcacion(StreamD, D(4, 50)),
            CoberturaVecinaActualizada.Crear(StreamD, DiaVecino.Vispera, D(5, 0)));
    }

    private static AmbiguedadDeMarcacionesResuelta ResolucionDeD(params MarcacionDecidida[] marcaciones) =>
        AmbiguedadDeMarcacionesResuelta.Crear(StreamD, DiaVecino.Vispera, marcaciones);

    private static AmbiguedadDeMarcacionesResuelta ResolucionDeV(params MarcacionDecidida[] marcaciones) =>
        AmbiguedadDeMarcacionesResuelta.Crear(StreamV, DiaVecino.DiaSiguiente, marcaciones);

    private static DepuracionDiaRecibida Foto(
        string streamDc, DateOnly fecha, TimeOnly inicio, TimeOnly fin, int offsetFin,
        DateTime? entrada, DateTime? salida, params DateTime[] marcaciones) =>
        new(streamDc, CodigoColaborador, fecha, null, "Turno",
            [new FranjaDepurada(inicio, fin, offsetFin, entrada, salida, salida is null)],
            marcaciones.Select(m => new MarcacionDelDia(m, "ENTRADA")).ToList(),
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []));

    private void GivenVisperaAprobadaConLa0430(bool laUso)
    {
        var foto = laUso
            ? Foto(StreamDcV, Dia20, new TimeOnly(17, 0), new TimeOnly(1, 0), 1, V(16, 55), D(4, 30), V(16, 55), D(4, 30))
            : Foto(StreamDcV, Dia20, new TimeOnly(17, 0), new TimeOnly(1, 0), 1, V(16, 55), null, V(16, 55));
        Given(StreamDcV, foto, DiaAprobado.Crear(StreamDcV, CodigoColaborador, Dia20, []));
    }

    private void GivenDiaAprobado(bool laUso)
    {
        var foto = laUso
            ? Foto(StreamDcD, Dia21, new TimeOnly(6, 0), new TimeOnly(14, 0), 0, D(4, 30), D(14, 5), D(4, 30), D(14, 5))
            : Foto(StreamDcD, Dia21, new TimeOnly(6, 0), new TimeOnly(14, 0), 0, D(6, 20), D(14, 5), D(6, 20), D(14, 5));
        Given(StreamDcD, foto, DiaAprobado.Crear(StreamDcD, CodigoColaborador, Dia21, []));
    }

    // CA-1: la 04:30 a favor de la vispera: la vispera cierra con salida 04:30 y el dia la cede.
    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_DaLaMarcacionALaVispera_CuandoElRepartoLaAsignaAEsaFecha()
    {
        GivenSolapeDeLas0430();

        await WhenAsync(Comando(Decision(D(4, 30), Dia20)));

        var decidida = new MarcacionDecidida(D(4, 30), Dia20);
        Then(StreamD, ResolucionDeD(decidida));
        Then(StreamV, ResolucionDeV(decidida));
        And<ControlDiarioAggregateRoot, string>(StreamV,
            c => PertenenciasPorHora(c.CrearDiaDepurado()), "16:55=DelTurno 04:30=DelTurno");
        And<ControlDiarioAggregateRoot, DateTime?>(StreamV,
            c => c.ControlesDeFranja.Single().Entrada, V(16, 55));
        And<ControlDiarioAggregateRoot, DateTime?>(StreamV,
            c => c.ControlesDeFranja.Single().Salida, D(4, 30));
        And<ControlDiarioAggregateRoot, bool>(StreamV,
            c => c.ControlesDeFranja.Single().EsAnomala, false);
        And<ControlDiarioAggregateRoot, string>(StreamD,
            c => PertenenciasPorHora(c.CrearDiaDepurado()), "04:30=FueraDeCobertura 06:20=DelTurno 14:05=DelTurno");
        And<ControlDiarioAggregateRoot, DateTime?>(StreamD,
            c => c.ControlesDeFranja.Single().Entrada, D(6, 20));
        And<ControlDiarioAggregateRoot, DateTime?>(StreamD,
            c => c.ControlesDeFranja.Single().Salida, D(14, 5));
        FechasPublicadas().Should().Equal(Dia20, Dia21);
    }

    // CA-2: un solo acto reparte 04:00 a la vispera y 04:50 al dia.
    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_RepartePorMarcacion_CuandoHayDosEnElSolape()
    {
        GivenSolapeDeDosMarcaciones();

        await WhenAsync(Comando(Decision(D(4, 0), Dia20), Decision(D(4, 50), Dia21)));

        var decididas = new[] { new MarcacionDecidida(D(4, 0), Dia20), new MarcacionDecidida(D(4, 50), Dia21) };
        Then(StreamD, ResolucionDeD(decididas));
        Then(StreamV, ResolucionDeV(decididas));
        And<ControlDiarioAggregateRoot, DateTime?>(StreamV,
            c => c.ControlesDeFranja.Single().Salida, D(4, 0));
        And<ControlDiarioAggregateRoot, string>(StreamV,
            c => PertenenciasPorHora(c.CrearDiaDepurado()), "16:55=DelTurno 04:00=DelTurno 04:50=FueraDeCobertura");
        And<ControlDiarioAggregateRoot, DateTime?>(StreamD,
            c => c.ControlesDeFranja.Single().Entrada, D(4, 50));
        And<ControlDiarioAggregateRoot, string>(StreamD,
            c => PertenenciasPorHora(c.CrearDiaDepurado()), "04:00=FueraDeCobertura 04:50=DelTurno");
    }

    // CA-3: el reparto omite una marcacion ambigua.
    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_Declina_CuandoElRepartoOmiteUnaAmbigua()
    {
        GivenSolapeDeDosMarcaciones();

        var act = async () => await WhenAsync(Comando(Decision(D(4, 0), Dia20)));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{ResolverAmbiguedadDeMarcacionesCommandHandler.Mensajes.RepartoIncompleto}*");
        Then(StreamD);
        Then(StreamV);
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
        And<ControlDiarioAggregateRoot, string>(StreamD,
            c => PertenenciasPorHora(c.CrearDiaDepurado()), "04:00=Ambigua 04:50=Ambigua");
    }

    // CA-3: incluye una marcacion que no es ambigua (06:20 es DelTurno).
    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_Declina_CuandoElRepartoIncluyeUnaMarcacionNoAmbigua()
    {
        GivenSolapeDeLas0430();

        var act = async () => await WhenAsync(
            Comando(Decision(D(4, 30), Dia20), Decision(D(6, 20), Dia21)));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{ResolverAmbiguedadDeMarcacionesCommandHandler.Mensajes.MarcacionNoAmbigua}*");
        Then(StreamD);
        Then(StreamV);
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
        And<ControlDiarioAggregateRoot, string>(StreamD,
            c => PertenenciasPorHora(c.CrearDiaDepurado()), "04:30=Ambigua 06:20=DelTurno 14:05=DelTurno");
    }

    // CA-3: la fecha no es el dia abierto ni el vecino en disputa.
    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_Declina_CuandoLaFechaNoEsElDiaAbiertoNiElVecinoEnDisputa()
    {
        GivenSolapeDeLas0430();

        var act = async () => await WhenAsync(
            Comando(Decision(D(4, 30), new DateOnly(2026, 10, 19))));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{ResolverAmbiguedadDeMarcacionesCommandHandler.Mensajes.FechaNoValida}*");
        Then(StreamD);
        Then(StreamV);
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
        And<ControlDiarioAggregateRoot, string>(StreamD,
            c => PertenenciasPorHora(c.CrearDiaDepurado()), "04:30=Ambigua 06:20=DelTurno 14:05=DelTurno");
    }

    // CA-4: sin stream cd: del dia abierto.
    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_LanzaRecursoNoEncontradoException_CuandoElDiaNoTieneDepuracion()
    {
        var act = async () => await WhenAsync(Comando(Decision(D(4, 30), Dia20)));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{ResolverAmbiguedadDeMarcacionesCommandHandler.Mensajes.DiaSinDepuracion}*");
    }

    // CA-5: repetir el mismo reparto no emite eventos ni publica.
    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_NoEmiteEventosNiPublica_CuandoSeRepiteElMismoReparto()
    {
        GivenSolapeDeLas0430(resueltaAntes: new MarcacionDecidida(D(4, 30), Dia20));

        await WhenAsync(Comando(Decision(D(4, 30), Dia20)));

        Then(StreamD);
        Then(StreamV);
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
        And<ControlDiarioAggregateRoot, string>(StreamV,
            c => PertenenciasPorHora(c.CrearDiaDepurado()), "16:55=DelTurno 04:30=DelTurno");
        And<ControlDiarioAggregateRoot, string>(StreamD,
            c => PertenenciasPorHora(c.CrearDiaDepurado()), "04:30=FueraDeCobertura 06:20=DelTurno 14:05=DelTurno");
    }

    // CA-5: un reparto distinto con los dos dias provisionales es una decision nueva.
    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_AceptaLaDecisionNueva_CuandoElRepartoCambiaYLosDiasSonProvisionales()
    {
        GivenSolapeDeLas0430(resueltaAntes: new MarcacionDecidida(D(4, 30), Dia20));

        await WhenAsync(Comando(Decision(D(4, 30), Dia21)));

        var nueva = new MarcacionDecidida(D(4, 30), Dia21);
        Then(StreamD, ResolucionDeD(nueva));
        Then(StreamV, ResolucionDeV(nueva));
        And<ControlDiarioAggregateRoot, string>(StreamD,
            c => PertenenciasPorHora(c.CrearDiaDepurado()), "04:30=DelTurno 06:20=DelTurno 14:05=DelTurno");
        And<ControlDiarioAggregateRoot, string>(StreamV,
            c => PertenenciasPorHora(c.CrearDiaDepurado()), "16:55=DelTurno 04:30=FueraDeCobertura");
        FechasPublicadas().Should().Equal(Dia20, Dia21);
    }

    // CA-6: la vispera aprobada uso la 04:30; resolver a su favor no cambia su uso.
    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_Acepta_CuandoLaVisperaAprobadaUsoLaMarcacionYSeResuelveASuFavor()
    {
        GivenSolapeDeLas0430();
        GivenVisperaAprobadaConLa0430(laUso: true);

        await WhenAsync(Comando(Decision(D(4, 30), Dia20)));

        var decidida = new MarcacionDecidida(D(4, 30), Dia20);
        Then(StreamD, ResolucionDeD(decidida));
        Then(StreamV, ResolucionDeV(decidida));
        Then(StreamDcV);
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamDcV, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }

    // CA-6: la vispera aprobada uso la 04:30; resolver a favor del dia cambiaria su uso.
    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_Declina_CuandoLaVisperaAprobadaUsoLaMarcacionYSeResuelveAFavorDelDia()
    {
        GivenSolapeDeLas0430();
        GivenVisperaAprobadaConLa0430(laUso: true);

        var act = async () => await WhenAsync(Comando(Decision(D(4, 30), Dia21)));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{ResolverAmbiguedadDeMarcacionesCommandHandler.Mensajes.CambiaUsoEnDiaAprobado}*");
        Then(StreamD);
        Then(StreamV);
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamDcV, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }

    // CA-6: la foto aprobada de la vispera no uso la 04:30; resolver a favor del dia lo respeta.
    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_Acepta_CuandoLaVisperaAprobadaNoUsoLaMarcacionYSeResuelveAFavorDelDia()
    {
        GivenSolapeDeLas0430();
        GivenVisperaAprobadaConLa0430(laUso: false);

        await WhenAsync(Comando(Decision(D(4, 30), Dia21)));

        var decidida = new MarcacionDecidida(D(4, 30), Dia21);
        Then(StreamD, ResolucionDeD(decidida));
        Then(StreamV, ResolucionDeV(decidida));
        Then(StreamDcV);
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamDcV, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }

    // CA-6: la foto aprobada de la vispera no uso la 04:30; resolver a su favor le cambiaria el uso.
    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_Declina_CuandoLaVisperaAprobadaNoUsoLaMarcacionYSeResuelveASuFavor()
    {
        GivenSolapeDeLas0430();
        GivenVisperaAprobadaConLa0430(laUso: false);

        var act = async () => await WhenAsync(Comando(Decision(D(4, 30), Dia20)));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{ResolverAmbiguedadDeMarcacionesCommandHandler.Mensajes.CambiaUsoEnDiaAprobado}*");
        Then(StreamD);
        Then(StreamV);
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamDcV, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }

    // CA-6: con los dos dias aprobados siempre declina, aunque el reparto respete la foto de la vispera.
    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_Declina_CuandoLosDosDiasEstanAprobados()
    {
        GivenSolapeDeLas0430();
        GivenVisperaAprobadaConLa0430(laUso: true);
        GivenDiaAprobado(laUso: false);

        var act = async () => await WhenAsync(Comando(Decision(D(4, 30), Dia20)));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{ResolverAmbiguedadDeMarcacionesCommandHandler.Mensajes.CambiaUsoEnDiaAprobado}*");
        Then(StreamD);
        Then(StreamV);
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(StreamDcD, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }
}
