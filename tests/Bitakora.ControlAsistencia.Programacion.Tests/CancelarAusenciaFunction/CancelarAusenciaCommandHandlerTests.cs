using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.CancelarAusenciaFunction;
using Bitakora.ControlAsistencia.Programacion.CancelarAusenciaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CancelarAusenciaFunction;

public class CancelarAusenciaCommandHandlerTests : CommandHandlerAsyncTest<CancelarAusencia>
{
    private const string Codigo = "E001";
    private const string StreamId = "ac:E001";

    private static readonly Guid AusenciaId = Guid.Parse("019600a0-0000-7000-8000-000000000744");
    private static readonly Guid OtraAusenciaId = Guid.Parse("019600a0-0000-7000-8000-000000000745");

    private static readonly ColaboradorProgramado Colaborador = new("CC-12345678", Codigo, "Ana Maria Gomez");
    private static readonly ResumenColaborador Resumen = new("CC-12345678", Codigo, "Ana Maria Gomez");

    protected override ICommandHandlerAsync<CancelarAusencia> Handler =>
        new CancelarAusenciaCommandHandler(EventStore, PrivateEventSender);

    private static DateOnly Oct(int dia) => new(2026, 10, dia);

    private static CancelarAusencia Comando(Guid? id = null, params int[] dias) =>
        new(Codigo, id ?? AusenciaId, [.. dias.Select(Oct)]);

    private static AusenciaProgramada Del13Al26() =>
        new(AusenciaId, Colaborador, Oct(13), Oct(26), MotivoAusencia.IncapacidadMedica);

    private static AusenciaDiariaCancelada Diaria(int dia) => new(AusenciaId, Resumen, Oct(dia));

    private static string Tramos(AusenciasColaborador a) =>
        string.Join(",", a.ListarAusenciasVigentes(Oct(1), Oct(31)).Single().TramosVigentes
            .Select(t => $"{t.Desde.Day}-{t.Hasta.Day}"));

    // CA-1
    [Fact]
    public async Task CancelarAusencia_EmiteEventoYPublicaUnoPorFecha_CuandoLasFechasEstanVigentes()
    {
        Given(StreamId, Del13Al26());

        await WhenAsync(Comando(null, 20, 21, 22));

        Then(StreamId, new AusenciaCancelada(AusenciaId, Colaborador, [Oct(20), Oct(21), Oct(22)]));
        ThenIsPublishedPrivately(Diaria(20), Diaria(21), Diaria(22));
        And<AusenciasColaborador, string>(StreamId, Tramos, "13-19,23-26");
    }

    // CA-2
    [Fact]
    public async Task CancelarAusencia_EmiteSoloLasVigentes_CuandoLaPeticionMezclaVigentesCanceladasYAjenas()
    {
        Given(StreamId, Del13Al26(), new AusenciaCancelada(AusenciaId, Colaborador, [Oct(26)]));

        await WhenAsync(Comando(null, 25, 26, 27, 28));

        Then(StreamId, new AusenciaCancelada(AusenciaId, Colaborador, [Oct(25)]));
        ThenIsPublishedPrivately(Diaria(25));
        And<AusenciasColaborador, string>(StreamId, Tramos, "13-24");
    }

    [Fact]
    public async Task CancelarAusencia_TrataUnaSolaVezLasFechasRepetidas_CuandoLaListaTraeDuplicados()
    {
        Given(StreamId, Del13Al26());

        await WhenAsync(Comando(null, 20, 20, 21, 20));

        Then(StreamId, new AusenciaCancelada(AusenciaId, Colaborador, [Oct(20), Oct(21)]));
        ThenIsPublishedPrivately(Diaria(20), Diaria(21));
        And<AusenciasColaborador, string>(StreamId, Tramos, "13-19,22-26");
    }

    [Fact]
    public async Task CancelarAusencia_ConservaLaTernaDelColaboradorDeLaProgramacion_CuandoCancelaSucesivamente()
    {
        Given(StreamId, Del13Al26(), new AusenciaCancelada(AusenciaId, Colaborador, [Oct(20)]));

        await WhenAsync(Comando(null, 21));

        Then(StreamId, new AusenciaCancelada(AusenciaId, Colaborador, [Oct(21)]));
        ThenIsPublishedPrivately(Diaria(21));
        And<AusenciasColaborador, string>(StreamId, Tramos, "13-19,22-26");
    }

    [Fact]
    public async Task CancelarAusencia_NoTocaOtrasAusencias_CuandoElStreamTieneVarias()
    {
        Given(StreamId, Del13Al26(),
            new AusenciaProgramada(OtraAusenciaId, Colaborador, Oct(1), Oct(5), MotivoAusencia.Vacaciones));

        await WhenAsync(Comando(null, 3, 14));

        Then(StreamId, new AusenciaCancelada(AusenciaId, Colaborador, [Oct(14)]));
        ThenIsPublishedPrivately(Diaria(14));
        And<AusenciasColaborador, int>(StreamId, a => a.Ausencias.Count, 2);
    }

    // CA-3
    [Fact]
    public async Task CancelarAusencia_NoEmiteNiPublicaNada_CuandoTodasLasFechasYaEstanCanceladas()
    {
        Given(StreamId, Del13Al26(), new AusenciaCancelada(AusenciaId, Colaborador, [Oct(20), Oct(21)]));

        await WhenAsync(Comando(null, 20, 21));

        Then(StreamId, Array.Empty<object>());
        ThenIsPublishedPrivately();
        And<AusenciasColaborador, string>(StreamId, Tramos, "13-19,22-26");
    }

    [Fact]
    public async Task CancelarAusencia_NoEmiteNiPublicaNada_CuandoTodasLasFechasSonAjenasALaAusencia()
    {
        Given(StreamId, Del13Al26());

        await WhenAsync(Comando(null, 1, 2, 27));

        Then(StreamId, Array.Empty<object>());
        ThenIsPublishedPrivately();
        And<AusenciasColaborador, string>(StreamId, Tramos, "13-26");
    }

    // CA-4
    [Fact]
    public async Task CancelarAusencia_LanzaRecursoNoEncontradoException_CuandoElIdNoExisteParaElColaborador()
    {
        Given(StreamId, Del13Al26());

        var act = async () => await WhenAsync(Comando(OtraAusenciaId, 20));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{CancelarAusenciaCommandHandler.Mensajes.AusenciaNoEncontrada}*");
        Then(StreamId, Array.Empty<object>());
        ThenIsPublishedPrivately();
        And<AusenciasColaborador, string>(StreamId, Tramos, "13-26");
    }

    [Fact]
    public async Task CancelarAusencia_LanzaRecursoNoEncontradoException_CuandoElColaboradorNoTieneAusencias()
    {
        var act = async () => await WhenAsync(Comando(null, 20));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{CancelarAusenciaCommandHandler.Mensajes.AusenciaNoEncontrada}*");
        Then(StreamId, Array.Empty<object>());
        ThenIsPublishedPrivately();
    }
}
