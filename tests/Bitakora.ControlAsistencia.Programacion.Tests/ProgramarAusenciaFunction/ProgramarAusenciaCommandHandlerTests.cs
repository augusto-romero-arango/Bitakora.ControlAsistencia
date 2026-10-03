using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.ProgramarAusenciaFunction;
using Bitakora.ControlAsistencia.Programacion.ProgramarAusenciaFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ProgramarAusenciaFunction;

public class ProgramarAusenciaCommandHandlerTests : CommandHandlerAsyncTest<ProgramarAusencia>
{
    private const string Codigo = "E001";
    private const string StreamId = "ac:E001";

    private static readonly Guid AusenciaId = Guid.Parse("019600a0-0000-7000-8000-000000000743");
    private static readonly Guid AusenciaPreviaId = Guid.Parse("019600a0-0000-7000-8000-000000000744");

    private static readonly ColaboradorProgramado ColaboradorEsperado =
        new("CC-12345678", Codigo, "Ana Maria Gomez");

    private static readonly ResumenColaborador ColaboradorResumen =
        new("CC-12345678", Codigo, "Ana Maria Gomez");

    protected override ICommandHandlerAsync<ProgramarAusencia> Handler =>
        new ProgramarAusenciaCommandHandler(EventStore, PrivateEventSender);

    private static ProgramarAusencia Comando(
        DateOnly inicio, DateOnly fin, string motivo = "Vacaciones", Guid? id = null) =>
        new(id ?? AusenciaId, Codigo, "CC-12345678", "Ana Maria Gomez", inicio, fin, motivo);

    private static AusenciaProgramada AusenciaPrevia(DateOnly inicio, DateOnly fin) =>
        new(AusenciaPreviaId, ColaboradorEsperado, inicio, fin, MotivoAusencia.Vacaciones);

    // CA-1
    [Fact]
    public async Task ProgramarAusencia_IniciaElStreamYPublicaUnEventoPorFecha_CuandoElColaboradorNoTieneAusencias()
    {
        await WhenAsync(Comando(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 7)));

        Then(StreamId, new AusenciaProgramada(
            AusenciaId, ColaboradorEsperado, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 7),
            MotivoAusencia.Vacaciones));
        ThenIsPublishedPrivately(
            new AusenciaDiariaProgramada(AusenciaId, ColaboradorResumen, new DateOnly(2026, 10, 5), "Vacaciones"),
            new AusenciaDiariaProgramada(AusenciaId, ColaboradorResumen, new DateOnly(2026, 10, 6), "Vacaciones"),
            new AusenciaDiariaProgramada(AusenciaId, ColaboradorResumen, new DateOnly(2026, 10, 7), "Vacaciones"));
        And<AusenciasColaborador, int>(StreamId, a => a.Ausencias.Count, 1);
    }

    [Fact]
    public async Task ProgramarAusencia_PublicaUnSoloEvento_CuandoInicioYFinSonLaMismaFecha()
    {
        await WhenAsync(Comando(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5), "IncapacidadMedica"));

        Then(StreamId, new AusenciaProgramada(
            AusenciaId, ColaboradorEsperado, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5),
            MotivoAusencia.IncapacidadMedica));
        ThenIsPublishedPrivately(
            new AusenciaDiariaProgramada(
                AusenciaId, ColaboradorResumen, new DateOnly(2026, 10, 5), "IncapacidadMedica"));
        And<AusenciasColaborador, int>(StreamId, a => a.Ausencias.Count, 1);
    }

    [Fact]
    public async Task ProgramarAusencia_AceptaMotivoSinImportarMayusculas_CuandoElTextoVieneEnMinusculas()
    {
        await WhenAsync(Comando(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5), "licenciaremunerada"));

        Then(StreamId, new AusenciaProgramada(
            AusenciaId, ColaboradorEsperado, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5),
            MotivoAusencia.LicenciaRemunerada));
        ThenIsPublishedPrivately(
            new AusenciaDiariaProgramada(
                AusenciaId, ColaboradorResumen, new DateOnly(2026, 10, 5), "LicenciaRemunerada"));
        And<AusenciasColaborador, int>(StreamId, a => a.Ausencias.Count, 1);
    }

    // CA-2
    [Fact]
    public async Task ProgramarAusencia_AgregaAlMismoStream_CuandoElRangoNoTocaLaAusenciaPrevia()
    {
        Given(StreamId, AusenciaPrevia(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 19)));

        await WhenAsync(Comando(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 21)));

        Then(StreamId, new AusenciaProgramada(
            AusenciaId, ColaboradorEsperado, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 21),
            MotivoAusencia.Vacaciones));
        ThenIsPublishedPrivately(
            new AusenciaDiariaProgramada(AusenciaId, ColaboradorResumen, new DateOnly(2026, 10, 20), "Vacaciones"),
            new AusenciaDiariaProgramada(AusenciaId, ColaboradorResumen, new DateOnly(2026, 10, 21), "Vacaciones"));
        And<AusenciasColaborador, int>(StreamId, a => a.Ausencias.Count, 2);
    }

    // CA-2: el rango previo termina antes de que empiece el nuevo, del otro lado.
    [Fact]
    public async Task ProgramarAusencia_AgregaAlMismoStream_CuandoElRangoNuevoTerminaJustoAntesDeLaPrevia()
    {
        Given(StreamId, AusenciaPrevia(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 25)));

        await WhenAsync(Comando(new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 19)));

        Then(StreamId, new AusenciaProgramada(
            AusenciaId, ColaboradorEsperado, new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 19),
            MotivoAusencia.Vacaciones));
        ThenIsPublishedPrivately(
            Enumerable.Range(15, 5)
                .Select(dia => new AusenciaDiariaProgramada(
                    AusenciaId, ColaboradorResumen, new DateOnly(2026, 10, dia), "Vacaciones"))
                .ToArray());
        And<AusenciasColaborador, int>(StreamId, a => a.Ausencias.Count, 2);
    }

    // CA-3
    [Fact]
    public async Task ProgramarAusencia_DeclinaConLasFechasEnConflicto_CuandoElRangoComparteFechasConUnaAusenciaVigente()
    {
        Given(StreamId, AusenciaPrevia(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 19)));

        var act = async () => await WhenAsync(
            Comando(new DateOnly(2026, 10, 17), new DateOnly(2026, 10, 21), "IncapacidadMedica"));

        var excepcion = (await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()).Which;
        excepcion.Message.Should().Be(ProgramarAusenciaCommandHandler.Mensajes.ChoqueConAusencia(
            [new DateOnly(2026, 10, 17), new DateOnly(2026, 10, 18), new DateOnly(2026, 10, 19)],
            MotivoAusencia.Vacaciones));
        excepcion.Message.Should().Contain("2026-10-17").And.Contain("2026-10-18")
            .And.Contain("2026-10-19").And.Contain("Vacaciones");
        excepcion.Message.Should().NotContain("2026-10-20");
        Then(StreamId, Array.Empty<object>());
        ThenIsPublishedPrivately();
        And<AusenciasColaborador, int>(StreamId, a => a.Ausencias.Count, 1);
    }

    [Fact]
    public async Task ProgramarAusencia_DeclinaConUnaSolaFecha_CuandoSoloSeTocaElUltimoDiaDeLaPrevia()
    {
        Given(StreamId, AusenciaPrevia(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 19)));

        var act = async () => await WhenAsync(Comando(new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25)));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{ProgramarAusenciaCommandHandler.Mensajes.ChoqueConAusencia([new DateOnly(2026, 10, 19)], MotivoAusencia.Vacaciones)}*");
        Then(StreamId, Array.Empty<object>());
        ThenIsPublishedPrivately();
        And<AusenciasColaborador, int>(StreamId, a => a.Ausencias.Count, 1);
    }

    [Fact]
    public async Task ProgramarAusencia_DeclinaSinRegistrarNada_CuandoElNuevoRangoContieneALaPrevia()
    {
        Given(StreamId, AusenciaPrevia(new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 15)));

        var act = async () => await WhenAsync(Comando(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{ProgramarAusenciaCommandHandler.Mensajes.ChoqueConAusencia([new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 15)], MotivoAusencia.Vacaciones)}*");
        Then(StreamId, Array.Empty<object>());
        ThenIsPublishedPrivately();
        And<AusenciasColaborador, int>(StreamId, a => a.Ausencias.Count, 1);
    }

    // CA-4
    [Fact]
    public async Task ProgramarAusencia_LanzaRecursoYaExisteException_CuandoElIdYaEstaRegistradoParaElColaborador()
    {
        Given(StreamId, AusenciaPrevia(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 19)));

        var act = async () => await WhenAsync(
            Comando(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3), id: AusenciaPreviaId));

        await act.Should().ThrowExactlyAsync<RecursoYaExisteException>()
            .WithMessage($"*{ProgramarAusenciaCommandHandler.Mensajes.AusenciaYaExiste}*");
        Then(StreamId, Array.Empty<object>());
        ThenIsPublishedPrivately();
        And<AusenciasColaborador, int>(StreamId, a => a.Ausencias.Count, 1);
    }

    // CA-6
    [Fact]
    public async Task ProgramarAusencia_PublicaCientoVeintiseisEventos_CuandoElRangoEsLargoYEstaEnElPasado()
    {
        var inicio = new DateOnly(2020, 1, 1);
        var fin = inicio.AddDays(125);

        await WhenAsync(Comando(inicio, fin, "AusenciaNoRemunerada"));

        Then(StreamId, new AusenciaProgramada(
            AusenciaId, ColaboradorEsperado, inicio, fin, MotivoAusencia.AusenciaNoRemunerada));
        ThenIsPublishedPrivately(
            Enumerable.Range(0, 126)
                .Select(i => new AusenciaDiariaProgramada(
                    AusenciaId, ColaboradorResumen, inicio.AddDays(i), "AusenciaNoRemunerada"))
                .ToArray());
        And<AusenciasColaborador, int>(StreamId, a => a.Ausencias.Count, 1);
    }
}
