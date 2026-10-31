using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.RepartirMarcacionesCuandoDiaDepuradoFunction;
using FunctionEndpoint = Bitakora.ControlAsistencia.ControlHoras.ActualizarControlDiario.FunctionEndpoint;
using Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.PrivateEvents.Sedes;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ActualizarControlDiario;

public class FunctionEndpointTests
{
    private const string Colaborador = "EMP-001";
    private const string Ns = "Bitakora.ControlAsistencia.PrivateEvents.";

    private static readonly JsonSerializerOptions OpcionesWeb = new(JsonSerializerDefaults.Web);
    private static readonly ResumenColaborador Resumen = new("CC-1234567890", Colaborador, "Luis Augusto Barreto");
    private static readonly Guid Id = Guid.Parse("019600b0-0000-7000-8000-000000000001");

    private const string JsonProgramacion = """
        {
          "solicitudId": "019600b0-0000-7000-8000-000000000001",
          "colaborador": { "identificacion": "CC-1234567890", "codigoColaborador": "EMP-001", "nombreCompleto": "Luis Augusto Barreto" },
          "fecha": "2026-03-15",
          "detalleTurno": {
            "nombre": "Turno Manana",
            "franjasOrdinarias": [
              { "horaInicio": "08:00:00", "horaFin": "16:00:00", "diaOffsetFin": 0, "descansos": [], "extras": [] }
            ]
          }
        }
        """;

    private const string JsonDiaDepurado = """
        {
          "codigoColaborador": "EMP-001",
          "fecha": "2026-03-15",
          "colaborador": null,
          "nombreTurno": null,
          "franjas": [],
          "marcaciones": [],
          "horasDiscriminadas": { "horasPorConcepto": {}, "trazabilidad": [] }
        }
        """;

    private static ServiceBusReceivedMessage CrearMensaje(
        string? subject, string cuerpo, string messageId = "msg-1", int deliveryCount = 1)
        => ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(cuerpo), messageId: messageId, sessionId: Colaborador,
            subject: subject, deliveryCount: deliveryCount);

    private static string Json(object evento) => JsonSerializer.Serialize(evento, OpcionesWeb);

    private sealed class Escenario
    {
        public FakeRouterEventosPrivados RouterEventos { get; }
        public FakeCommandRouter RouterComandos { get; }
        public FakeServiceBusMessageActions Acciones { get; }
        public FakeLogger Logger { get; } = new();
        public FunctionEndpoint Endpoint { get; }

        public Escenario(
            Exception? falloRouterEventos = null, Exception? falloRouterComandos = null,
            Exception? falloAlCompletar = null)
        {
            RouterEventos = new FakeRouterEventosPrivados(falloRouterEventos);
            RouterComandos = new FakeCommandRouter(falloRouterComandos);
            Acciones = new FakeServiceBusMessageActions(falloAlCompletar);
            Endpoint = new FunctionEndpoint(RouterEventos, RouterComandos, new FakeLoggerTipado(Logger));
        }

        public Task Ejecutar(ServiceBusReceivedMessage mensaje)
            => Endpoint.Run(mensaje, Acciones, CancellationToken.None);
    }

    [Fact]
    public void ActualizarControlDiario_SeDisparaDesdeElQueueConSesion_ConLaConexionDelBc()
    {
        var run = typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;

        run.GetCustomAttribute<FunctionAttribute>()!.Name.Should().Be("ActualizarControlDiario");
        var trigger = run.GetParameters()[0].GetCustomAttribute<ServiceBusTriggerAttribute>()!;
        trigger.QueueName.Should().Be("actualizar-control-diario");
        trigger.Connection.Should().Be("SERVICE_BUS_CONNECTION");
        trigger.IsSessionsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ActualizarControlDiario_DespachaRegistroDeMarcacionCreadoAlRouterDeEventos_CuandoElSubjectLoIdentifica()
    {
        var esc = new Escenario();
        var evento = new RegistroDeMarcacionCreado(Colaborador, new DateTime(2026, 3, 15, 8, 0, 0), "Entrada", "disp-1");

        await esc.Ejecutar(CrearMensaje(Ns + "ControlHoras.RegistroDeMarcacionCreado", Json(evento)));

        esc.RouterEventos.Eventos.Should().ContainSingle().Which.Should().Be(evento);
        esc.RouterComandos.Invocaciones.Should().Be(0);
        esc.Acciones.MensajeCompletado.Should().BeTrue();
    }

    [Fact]
    public async Task ActualizarControlDiario_DespachaSedeDeMarcacionResueltaAlRouterDeEventos_CuandoElSubjectLoIdentifica()
    {
        var esc = new Escenario();
        var evento = new SedeDeMarcacionResuelta(
            Colaborador, new DateTime(2026, 3, 15, 8, 0, 0), "disp-1", "SEDE-1", "Sede Norte", "CC-10");

        await esc.Ejecutar(CrearMensaje(Ns + "Sedes.SedeDeMarcacionResuelta", Json(evento)));

        esc.RouterEventos.Eventos.Should().ContainSingle().Which.Should().Be(evento);
        esc.RouterComandos.Invocaciones.Should().Be(0);
        esc.Acciones.MensajeCompletado.Should().BeTrue();
    }

    [Fact]
    public async Task ActualizarControlDiario_DespachaProgramacionTurnoDiarioSolicitadaAlRouterDeEventos_CuandoElSubjectLoIdentifica()
    {
        var esc = new Escenario();

        await esc.Ejecutar(CrearMensaje(Ns + "Programacion.ProgramacionTurnoDiarioSolicitada", JsonProgramacion));

        var evento = esc.RouterEventos.Eventos.Should().ContainSingle().Which
            .Should().BeOfType<ProgramacionTurnoDiarioSolicitada>().Subject;
        evento.SolicitudId.Should().Be(Id);
        evento.Fecha.Should().Be(new DateOnly(2026, 3, 15));
        esc.RouterComandos.Invocaciones.Should().Be(0);
        esc.Acciones.MensajeCompletado.Should().BeTrue();
    }

    [Fact]
    public async Task ActualizarControlDiario_DespachaProgramacionTurnoEnFechasSolicitadaAlRouterDeEventos_CuandoElSubjectLoIdentifica()
    {
        var esc = new Escenario();
        var evento = new ProgramacionTurnoEnFechasSolicitada(
            Id, Resumen, [new DateOnly(2026, 3, 15), new DateOnly(2026, 3, 16)],
            new DetalleTurno("Turno Manana",
                [new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "")], ""),
            new DetalleJornada(Id, 2880, 600, 0, 1));

        await esc.Ejecutar(CrearMensaje(Ns + "Programacion.ProgramacionTurnoEnFechasSolicitada", Json(evento)));

        var recibido = esc.RouterEventos.Eventos.Should().ContainSingle().Which
            .Should().BeOfType<ProgramacionTurnoEnFechasSolicitada>().Subject;
        recibido.SolicitudId.Should().Be(Id);
        recibido.Fechas.Should().Equal(new DateOnly(2026, 3, 15), new DateOnly(2026, 3, 16));
        esc.RouterComandos.Invocaciones.Should().Be(0);
        esc.Acciones.MensajeCompletado.Should().BeTrue();
    }

    [Fact]
    public async Task ActualizarControlDiario_DespachaDiasDepuradosAlRouterDeEventos_CuandoElSubjectLoIdentifica()
    {
        var esc = new Escenario();
        var evento = new DiasDepurados(Colaborador, [new DiaDepurado(
            Colaborador, new DateOnly(2026, 3, 15), null, null, [], [],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []))]);

        await esc.Ejecutar(CrearMensaje(Ns + "ControlHoras.DiasDepurados", Json(evento)));

        var recibido = esc.RouterEventos.Eventos.Should().ContainSingle().Which
            .Should().BeOfType<DiasDepurados>().Subject;
        recibido.CodigoColaborador.Should().Be(Colaborador);
        recibido.Dias.Should().ContainSingle().Which.Fecha.Should().Be(new DateOnly(2026, 3, 15));
        esc.RouterComandos.Invocaciones.Should().Be(0);
        esc.Acciones.MensajeCompletado.Should().BeTrue();
    }

    [Fact]
    public async Task ActualizarControlDiario_DespachaCancelacionTurnoDiarioSolicitadaAlRouterDeEventos_CuandoElSubjectLoIdentifica()
    {
        var esc = new Escenario();
        var evento = new CancelacionTurnoDiarioSolicitada(Id, Resumen, new DateOnly(2026, 3, 15));

        await esc.Ejecutar(CrearMensaje(Ns + "Programacion.CancelacionTurnoDiarioSolicitada", Json(evento)));

        var recibido = esc.RouterEventos.Eventos.Should().ContainSingle().Which
            .Should().BeOfType<CancelacionTurnoDiarioSolicitada>().Subject;
        recibido.SolicitudId.Should().Be(Id);
        recibido.Colaborador.Should().Be(Resumen);
        recibido.Fecha.Should().Be(new DateOnly(2026, 3, 15));
        esc.RouterComandos.Invocaciones.Should().Be(0);
        esc.Acciones.MensajeCompletado.Should().BeTrue();
    }

    [Fact]
    public async Task ActualizarControlDiario_DespachaAusenciaDiariaProgramadaAlRouterDeEventos_CuandoElSubjectLoIdentifica()
    {
        var esc = new Escenario();
        var evento = new AusenciaDiariaProgramada(Id, Resumen, new DateOnly(2026, 3, 15), "Vacaciones");

        await esc.Ejecutar(CrearMensaje(Ns + "Programacion.AusenciaDiariaProgramada", Json(evento)));

        var recibido = esc.RouterEventos.Eventos.Should().ContainSingle().Which
            .Should().BeOfType<AusenciaDiariaProgramada>().Subject;
        recibido.AusenciaId.Should().Be(Id);
        recibido.Colaborador.Should().Be(Resumen);
        recibido.Motivo.Should().Be("Vacaciones");
        esc.RouterComandos.Invocaciones.Should().Be(0);
        esc.Acciones.MensajeCompletado.Should().BeTrue();
    }

    [Fact]
    public async Task ActualizarControlDiario_DespachaAusenciaDiariaCanceladaAlRouterDeEventos_CuandoElSubjectLoIdentifica()
    {
        var esc = new Escenario();
        var evento = new AusenciaDiariaCancelada(Id, Resumen, new DateOnly(2026, 3, 15));

        await esc.Ejecutar(CrearMensaje(Ns + "Programacion.AusenciaDiariaCancelada", Json(evento)));

        var recibido = esc.RouterEventos.Eventos.Should().ContainSingle().Which
            .Should().BeOfType<AusenciaDiariaCancelada>().Subject;
        recibido.AusenciaId.Should().Be(Id);
        recibido.Colaborador.Should().Be(Resumen);
        recibido.Fecha.Should().Be(new DateOnly(2026, 3, 15));
        esc.RouterComandos.Invocaciones.Should().Be(0);
        esc.Acciones.MensajeCompletado.Should().BeTrue();
    }

    [Fact]
    public async Task ActualizarControlDiario_DespachaDiaDepuradoAlRouterDeComandosComoRepartirMarcaciones_CuandoElSubjectLoIdentifica()
    {
        var esc = new Escenario();

        await esc.Ejecutar(CrearMensaje(Ns + "ControlHoras.DiaDepurado", JsonDiaDepurado));

        esc.RouterComandos.Comandos.Should().Equal(
            new RepartirMarcaciones("EMP-001", new DateOnly(2026, 3, 15)));
        esc.RouterEventos.Eventos.Should().BeEmpty();
        esc.Acciones.MensajeCompletado.Should().BeTrue();
    }

    [Fact]
    public async Task ActualizarControlDiario_EnviaADeadLetterSinInvocarRouters_CuandoElSubjectEsDesconocido()
    {
        var esc = new Escenario();

        await esc.Ejecutar(CrearMensaje(Ns + "ControlHoras.EventoInexistente", JsonDiaDepurado));

        esc.Acciones.MensajeEnDeadLetter.Should().BeTrue();
        esc.Acciones.MensajeAbandonado.Should().BeFalse();
        esc.Acciones.MensajeCompletado.Should().BeFalse();
        esc.RouterEventos.Invocaciones.Should().Be(0);
        esc.RouterComandos.Invocaciones.Should().Be(0);
    }

    [Fact]
    public async Task ActualizarControlDiario_EnviaADeadLetterSinInvocarRouters_CuandoElMensajeNoTieneSubject()
    {
        var esc = new Escenario();

        await esc.Ejecutar(CrearMensaje(null, JsonDiaDepurado));

        esc.Acciones.MensajeEnDeadLetter.Should().BeTrue();
        esc.Acciones.MensajeAbandonado.Should().BeFalse();
        esc.RouterEventos.Invocaciones.Should().Be(0);
        esc.RouterComandos.Invocaciones.Should().Be(0);
    }

    [Fact]
    public async Task ActualizarControlDiario_EnviaADeadLetterSinInvocarRouters_CuandoElCuerpoNoEsJson()
    {
        var esc = new Escenario();

        await esc.Ejecutar(CrearMensaje(Ns + "ControlHoras.RegistroDeMarcacionCreado", "no-es-json"));

        esc.Acciones.MensajeEnDeadLetter.Should().BeTrue();
        esc.Acciones.MensajeAbandonado.Should().BeFalse();
        esc.RouterEventos.Invocaciones.Should().Be(0);
        esc.RouterComandos.Invocaciones.Should().Be(0);
    }

    [Fact]
    public async Task ActualizarControlDiario_EnviaADeadLetterSinInvocarRouters_CuandoElCuerpoDeDiaDepuradoNoDeserializa()
    {
        var esc = new Escenario();

        await esc.Ejecutar(CrearMensaje(Ns + "ControlHoras.DiaDepurado", "null"));

        esc.Acciones.MensajeEnDeadLetter.Should().BeTrue();
        esc.RouterEventos.Invocaciones.Should().Be(0);
        esc.RouterComandos.Invocaciones.Should().Be(0);
    }

    [Fact]
    public async Task ActualizarControlDiario_AbandonaElMensaje_CuandoElRouterDeEventosFalla()
    {
        var esc = new Escenario(falloRouterEventos: new InvalidOperationException("fallo"));
        var evento = new RegistroDeMarcacionCreado(Colaborador, new DateTime(2026, 3, 15, 8, 0, 0), "Entrada", "disp-1");

        await esc.Ejecutar(CrearMensaje(Ns + "ControlHoras.RegistroDeMarcacionCreado", Json(evento)));

        esc.Acciones.MensajeAbandonado.Should().BeTrue();
        esc.Acciones.MensajeEnDeadLetter.Should().BeFalse();
        esc.Acciones.MensajeCompletado.Should().BeFalse();
    }

    [Fact]
    public async Task ActualizarControlDiario_AbandonaElMensaje_CuandoElRouterDeComandosFalla()
    {
        var esc = new Escenario(falloRouterComandos: new InvalidOperationException("fallo"));

        await esc.Ejecutar(CrearMensaje(Ns + "ControlHoras.DiaDepurado", JsonDiaDepurado));

        esc.Acciones.MensajeAbandonado.Should().BeTrue();
        esc.Acciones.MensajeEnDeadLetter.Should().BeFalse();
        esc.Acciones.MensajeCompletado.Should().BeFalse();
    }

    [Fact]
    public async Task ActualizarControlDiario_SoloRegistraWarning_CuandoSePierdeElLockAlCompletar()
    {
        var lockLost = new ServiceBusException("Lock expirado", ServiceBusFailureReason.MessageLockLost);
        var esc = new Escenario(falloAlCompletar: lockLost);

        await esc.Ejecutar(CrearMensaje(Ns + "ControlHoras.DiaDepurado", JsonDiaDepurado));

        esc.Acciones.MensajeAbandonado.Should().BeFalse();
        esc.Acciones.MensajeEnDeadLetter.Should().BeFalse();
        esc.Logger.WarningLogueado.Should().BeTrue();
    }

    [Fact]
    public async Task ActualizarControlDiario_RegistraMessageIdSessionIdYDeliveryCount_CuandoAbandona()
    {
        var esc = new Escenario(falloRouterComandos: new InvalidOperationException("fallo"));

        await esc.Ejecutar(CrearMensaje(Ns + "ControlHoras.DiaDepurado", JsonDiaDepurado, "msg-1175", 4));

        esc.Logger.MensajesError.Should().ContainSingle()
            .Which.Should().Contain("msg-1175").And.Contain(Colaborador).And.Contain("4");
    }
}

internal class FakeRouterEventosPrivados(Exception? excepcion = null) : IPrivateEventRouter
{
    public int Invocaciones { get; private set; }
    public List<object> Eventos { get; } = [];

    public Task InvokeAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : class, IPrivateEvent
    {
        Invocaciones++;
        Eventos.Add(@event);
        if (excepcion is not null) throw excepcion;
        return Task.CompletedTask;
    }
}

internal class FakeLoggerTipado(FakeLogger interno) : Microsoft.Extensions.Logging.ILogger<FunctionEndpoint>
{
    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
        TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => interno.Log(logLevel, eventId, state, exception, formatter);

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}
