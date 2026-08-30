using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
// Alias de tipo: ResumenColaborador existe homonimo en ControlHoras.DomainEvents (payload por
// rol, MEF-ADR-0039 decision #6); este archivo publica/consume por el bus, asi que usa el de
// PrivateEvents.
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.CancelarTurnoCuandoCancelacionTurnoDiarioSolicitadaFunction;

// Issue #499: lado ControlHoras de "Cancelar Programacion" (#498). Consumidor puro
// (ServiceBusTrigger, sin comando espejo, MEF-ADR-0024 decision #8): el arrange siembra el
// ControlDiario publicando programacion-turno-diario-solicitada (mismo patron que
// AsignarTurnoViaSbSmokeTests) y, cuando el escenario lo pide, registra una marcacion via POST.
// El Act publica cancelacion-turno-diario-solicitada y se verifican los efectos del handler
// (persistencia de TurnoDiarioCancelado + republicacion de DiaDepurado) o su ausencia en los
// ramales de no-op (CA-3/CA-4).
public class CancelarTurnoCuandoCancelacionTurnoDiarioSolicitadaSmokeTests(
    ApiFixture api, ServiceBusFixture serviceBus, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string TopicProgramacionEntrada = "programacion-turno-diario-solicitada";
    private const string TopicCancelacionEntrada = "cancelacion-turno-diario-solicitada";
    private const string SuscripcionConsumidor = "control-horas-escucha-cancelacion";
    private const string TopicDiaDepurado = "dia-depurado";
    private const string SuscripcionSmokeTests = "smoke-tests";
    private const string RutaMarcaciones = "/api/control-horas/marcaciones";
    private const string SchemaControlHoras = "control_horas";
    private const string TipoEventoTurnoDiarioAsignado = "turno_diario_asignado";
    private const string TipoEventoTurnoDiarioCancelado = "turno_diario_cancelado";
    private const string TipoEventoMarcacionAdicionada = "marcacion_adicionada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Ausencia de evento tras un no-op (CA-3/CA-4): a diferencia de una idempotencia HTTP sincrona
    // (donde el handler ya corrio dentro del mismo request), aqui el Act es un publish a Service
    // Bus -- el trigger necesita tiempo de entrega antes de que el handler decida no hacer nada.
    // 15s da margen a esa latencia sin esperar el timeout completo de 30s reservado para los
    // asserts positivos.
    private static readonly TimeSpan TimeoutAusencia = TimeSpan.FromSeconds(15);

    private static string ComputarStreamId(string codigoColaborador, DateOnly fecha) =>
        $"cd:{codigoColaborador}:{fecha:yyyyMMdd}";

    private static object CrearEventoCancelacion(
        Guid solicitudCancelacionId, ResumenColaborador colaborador, DateOnly fecha) => new
        {
            SolicitudId = solicitudCancelacionId,
            Colaborador = colaborador,
            Fecha = fecha.ToString("yyyy-MM-dd")
        };

    private Task<HttpResponseMessage> PostMarcacionAsync(
        string codigoColaborador, DateTime timestamp, string dispositivoId) =>
        _client.PostAsJsonAsync(RutaMarcaciones, new
        {
            codigoColaborador,
            timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = "ENTRADA",
            dispositivoId
        }, TestContext.Current.CancellationToken);

    // Arrange compartido: siembra TurnoDiarioAsignado publicando al topic de Programacion, tal
    // como lo hace el productor real en produccion (mismo camino que AsignarTurnoViaSbSmokeTests).
    private async Task AsignarTurnoAsync(
        ResumenColaborador colaborador, DateOnly fecha, string nombreTurno)
    {
        var streamId = ComputarStreamId(colaborador.CodigoColaborador, fecha);
        var solicitudId = Guid.CreateVersion7();

        var programacionPayload = new
        {
            SolicitudId = solicitudId,
            Colaborador = colaborador,
            Fecha = fecha.ToString("yyyy-MM-dd"),
            DetalleTurno = new
            {
                Nombre = nombreTurno,
                FranjasOrdinarias = new[]
                {
                    new
                    {
                        HoraInicio = "08:00:00",
                        HoraFin = "16:00:00",
                        DiaOffsetFin = 0,
                        Descansos = Array.Empty<object>(),
                        Extras = Array.Empty<object>()
                    }
                }
            }
        };

        await serviceBus.PublishAsync(TopicProgramacionEntrada, programacionPayload, solicitudId.ToString());

        var turnoAsignado = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoTurnoDiarioAsignado, Timeout,
            campoJson: "SolicitudId", valorJson: solicitudId.ToString());

        turnoAsignado.Should().BeTrue(
            $"el evento {TipoEventoTurnoDiarioAsignado} deberia existir antes de intentar cancelar el turno");
    }

    // CA-1: dia con turno asignado y con marcaciones -> se persiste TurnoDiarioCancelado,
    // DetalleTurno queda null y se publica DiaDepurado con las marcaciones crudas sin desglose
    // (sin plan no hay depuracion -- reversion del extinto #422).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelacionTurnoDiarioSolicitada_CancelaElTurnoYRepublicaConMarcacionesCrudas_CuandoElDiaTieneTurnoYMarcaciones()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 5, 10);
        var streamId = ComputarStreamId(codigoColaborador, fecha);
        var colaborador = new ResumenColaborador(
            "CC-100200300", codigoColaborador, "[TEST] Smoke Cancelacion Con Marcaciones");

        await AsignarTurnoAsync(colaborador, fecha, "[TEST] Turno A Cancelar Con Marcaciones");

        var timestampEntrada = new DateTime(fecha, new TimeOnly(8, 3, 0), DateTimeKind.Utc);
        var marcacionResponse = await PostMarcacionAsync(
            codigoColaborador, timestampEntrada, "[TEST] DEV-SMOKE-CANCELACION-1");
        marcacionResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var marcacionPersistida = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoMarcacionAdicionada, Timeout,
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);

        marcacionPersistida.Should().BeTrue(
            $"el evento {TipoEventoMarcacionAdicionada} deberia existir antes de cancelar el turno");

        // Purge-before-act: el unico DiaDepurado que quede en la suscripcion tras el Act es el de
        // la cancelacion (descarta los que ya publicaron el turno y la marcacion).
        await serviceBus.PurgeAsync(TopicDiaDepurado, SuscripcionSmokeTests);

        var solicitudCancelacionId = Guid.CreateVersion7();
        await serviceBus.PublishAsync(
            TopicCancelacionEntrada,
            CrearEventoCancelacion(solicitudCancelacionId, colaborador, fecha),
            solicitudCancelacionId.ToString());

        var cancelado = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoTurnoDiarioCancelado, Timeout,
            campoJson: "SolicitudCancelacionId", valorJson: solicitudCancelacionId.ToString());

        cancelado.Should().BeTrue(
            $"el evento {TipoEventoTurnoDiarioCancelado} deberia existir en el stream {streamId}");

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaControlHoras, streamId, TipoEventoTurnoDiarioCancelado,
            "SolicitudCancelacionId", solicitudCancelacionId.ToString(), TimeSpan.FromSeconds(5));

        var colaboradorEsperado = new ColaboradorProgramado(
            "CC-100200300", codigoColaborador, "[TEST] Smoke Cancelacion Con Marcaciones");
        var colaboradorPersistido = eventoPersistido
            .GetProperty("Colaborador").Deserialize<ColaboradorProgramado>();
        colaboradorPersistido.Should().Be(colaboradorEsperado);

        var diaDepurado = await serviceBus.WaitForMessageAsync<DiaDepurado>(
            TopicDiaDepurado, SuscripcionSmokeTests,
            e => e.CodigoColaborador == codigoColaborador,
            Timeout);

        diaDepurado.Fecha.Should().Be(fecha);
        diaDepurado.NombreTurno.Should().BeNull(
            "el turno quedo cancelado -- sin plan no hay NombreTurno que reportar");
        diaDepurado.Franjas.Should().BeEmpty("sin plan no hay franjas que depurar");
        diaDepurado.Marcaciones.Should().ContainSingle(m => m.Timestamp == timestampEntrada);
        diaDepurado.HorasDiscriminadas.HorasPorConcepto.Should().BeEmpty(
            "sin plan no hay desglose de horas -- las marcaciones quedan crudas");

        var existeDeadLetter = await serviceBus.ExisteDeadLetterDeEstaCorridaAsync<CancelacionTurnoDiarioSolicitadaMinimo>(
            TopicCancelacionEntrada, SuscripcionConsumidor, e => e.SolicitudId == solicitudCancelacionId);

        existeDeadLetter.Should().BeFalse(
            "no deberia haber un dead letter de esta corrida (SolicitudId {0}) en '{1}' - si lo hay, el consumidor fallo al procesar el evento",
            solicitudCancelacionId, SuscripcionConsumidor);
    }

    // CA-2: dia con turno asignado y SIN marcaciones -> cancelado efectivo, DiaDepurado sin
    // franjas, sin marcaciones ni horas.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelacionTurnoDiarioSolicitada_CancelaElTurno_CuandoElDiaTieneTurnoYNoTieneMarcaciones()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 5, 11);
        var streamId = ComputarStreamId(codigoColaborador, fecha);
        var colaborador = new ResumenColaborador(
            "CC-200300400", codigoColaborador, "[TEST] Smoke Cancelacion Sin Marcaciones");

        await AsignarTurnoAsync(colaborador, fecha, "[TEST] Turno A Cancelar Sin Marcaciones");

        await serviceBus.PurgeAsync(TopicDiaDepurado, SuscripcionSmokeTests);

        var solicitudCancelacionId = Guid.CreateVersion7();
        await serviceBus.PublishAsync(
            TopicCancelacionEntrada,
            CrearEventoCancelacion(solicitudCancelacionId, colaborador, fecha),
            solicitudCancelacionId.ToString());

        var cancelado = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoTurnoDiarioCancelado, Timeout,
            campoJson: "SolicitudCancelacionId", valorJson: solicitudCancelacionId.ToString());

        cancelado.Should().BeTrue(
            $"el evento {TipoEventoTurnoDiarioCancelado} deberia existir en el stream {streamId}");

        var diaDepurado = await serviceBus.WaitForMessageAsync<DiaDepurado>(
            TopicDiaDepurado, SuscripcionSmokeTests,
            e => e.CodigoColaborador == codigoColaborador,
            Timeout);

        diaDepurado.Fecha.Should().Be(fecha);
        diaDepurado.NombreTurno.Should().BeNull();
        diaDepurado.Franjas.Should().BeEmpty();
        diaDepurado.Marcaciones.Should().BeEmpty("no hubo marcaciones antes de la cancelacion");
        diaDepurado.HorasDiscriminadas.HorasPorConcepto.Should().BeEmpty();
    }

    // CA-3: no-op silencioso -- el stream nunca existio (ni turno ni marcaciones para este
    // colaborador+fecha). Sin evento de constancia: la auditoria del acto quedo en Programacion.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelacionTurnoDiarioSolicitada_NoHaceNada_CuandoElStreamNoExiste()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 5, 12);
        var streamId = ComputarStreamId(codigoColaborador, fecha);
        var colaborador = new ResumenColaborador(
            "CC-300400500", codigoColaborador, "[TEST] Smoke Cancelacion Stream Inexistente");
        var solicitudCancelacionId = Guid.CreateVersion7();

        await serviceBus.PublishAsync(
            TopicCancelacionEntrada,
            CrearEventoCancelacion(solicitudCancelacionId, colaborador, fecha),
            solicitudCancelacionId.ToString());

        var cancelado = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoTurnoDiarioCancelado, TimeoutAusencia,
            campoJson: "SolicitudCancelacionId", valorJson: solicitudCancelacionId.ToString());

        cancelado.Should().BeFalse(
            "el stream nunca existio -- el no-op no deberia persistir ningun evento (CA-3, sin constancia)");

        var existeDeadLetter = await serviceBus.ExisteDeadLetterDeEstaCorridaAsync<CancelacionTurnoDiarioSolicitadaMinimo>(
            TopicCancelacionEntrada, SuscripcionConsumidor, e => e.SolicitudId == solicitudCancelacionId);

        existeDeadLetter.Should().BeFalse(
            "el no-op deberia resolverse sin error -- un dead letter aqui indicaria que el handler lanzo en vez de declinar");
    }

    // CA-4: no-op identico a CA-3, pero el stream SI existe (nacio solo por marcaciones, sin turno
    // asignado nunca). El aggregate declina con resultado (Tell-don't-Ask, MEF-ADR-0012): las
    // marcaciones existentes quedan intactas.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelacionTurnoDiarioSolicitada_NoHaceNada_CuandoElDiaExisteSinTurnoAsignado()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 5, 13);
        var streamId = ComputarStreamId(codigoColaborador, fecha);

        var timestampEntrada = new DateTime(fecha, new TimeOnly(7, 15, 0), DateTimeKind.Utc);
        var marcacionResponse = await PostMarcacionAsync(
            codigoColaborador, timestampEntrada, "[TEST] DEV-SMOKE-CANCELACION-4");
        marcacionResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var marcacionPersistida = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoMarcacionAdicionada, Timeout,
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);

        marcacionPersistida.Should().BeTrue(
            $"el evento {TipoEventoMarcacionAdicionada} deberia existir antes de intentar cancelar");

        var colaborador = new ResumenColaborador(
            "CC-400500600", codigoColaborador, "[TEST] Smoke Cancelacion Solo Marcaciones");
        var solicitudCancelacionId = Guid.CreateVersion7();

        await serviceBus.PublishAsync(
            TopicCancelacionEntrada,
            CrearEventoCancelacion(solicitudCancelacionId, colaborador, fecha),
            solicitudCancelacionId.ToString());

        var cancelado = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoTurnoDiarioCancelado, TimeoutAusencia,
            campoJson: "SolicitudCancelacionId", valorJson: solicitudCancelacionId.ToString());

        cancelado.Should().BeFalse(
            "el dia existe solo por marcaciones -- sin turno asignado, la cancelacion es no-op (CA-4)");

        var marcacionSigueIntacta = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoMarcacionAdicionada, TimeSpan.FromSeconds(5),
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);

        marcacionSigueIntacta.Should().BeTrue(
            "la marcacion previa no deberia desaparecer tras el intento de cancelacion no-op");

        var existeDeadLetter = await serviceBus.ExisteDeadLetterDeEstaCorridaAsync<CancelacionTurnoDiarioSolicitadaMinimo>(
            TopicCancelacionEntrada, SuscripcionConsumidor, e => e.SolicitudId == solicitudCancelacionId);

        existeDeadLetter.Should().BeFalse(
            "el no-op deberia resolverse sin error -- un dead letter aqui indicaria que el handler lanzo en vez de declinar");
    }
}
