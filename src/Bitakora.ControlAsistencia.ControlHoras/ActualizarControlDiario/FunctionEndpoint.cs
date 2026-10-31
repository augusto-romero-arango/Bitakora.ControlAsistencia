using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.ControlHoras.RepartirMarcacionesCuandoDiaDepuradoFunction;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.PrivateEvents.Sedes;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.ActualizarControlDiario;

public class FunctionEndpoint(
    IPrivateEventRouter privateEventRouter,
    ICommandRouter commandRouter,
    ILogger<FunctionEndpoint> logger)
    : ServiceBusSessionEndpointBase(logger)
{
    private delegate Func<CancellationToken, Task> Despachador(
        BinaryData body, IPrivateEventRouter eventos, ICommandRouter comandos);

    private static readonly IReadOnlyDictionary<string, Despachador> DespachadorPorSubject =
        new Dictionary<string, Despachador>
        {
            [typeof(RegistroDeMarcacionCreado).FullName!] = Evento<RegistroDeMarcacionCreado>,
            [typeof(SedeDeMarcacionResuelta).FullName!] = Evento<SedeDeMarcacionResuelta>,
            [typeof(ProgramacionTurnoDiarioSolicitada).FullName!] = Evento<ProgramacionTurnoDiarioSolicitada>,
            [typeof(ProgramacionTurnoEnFechasSolicitada).FullName!] = Evento<ProgramacionTurnoEnFechasSolicitada>,
            [typeof(CancelacionTurnoDiarioSolicitada).FullName!] = Evento<CancelacionTurnoDiarioSolicitada>,
            [typeof(AusenciaDiariaProgramada).FullName!] = Evento<AusenciaDiariaProgramada>,
            [typeof(AusenciaDiariaCancelada).FullName!] = Evento<AusenciaDiariaCancelada>,
            [typeof(DiaDepurado).FullName!] = DiaDepuradoComoComando,
        };

    [Function("ActualizarControlDiario")]
    public Task Run(
        [ServiceBusTrigger("actualizar-control-diario", Connection = "SERVICE_BUS_CONNECTION",
            IsSessionsEnabled = true)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
        => ProcesarMensajeDeSesion(message, messageActions, ct);

    protected override Func<CancellationToken, Task> Interpretar(ServiceBusReceivedMessage message)
    {
        if (!DespachadorPorSubject.TryGetValue(message.Subject ?? string.Empty, out var despachar))
            throw new InvalidOperationException(
                $"Subject no reconocido en el queue actualizar-control-diario: {message.Subject}");

        return despachar(message.Body, privateEventRouter, commandRouter);
    }

    private static Func<CancellationToken, Task> Evento<TEvento>(
        BinaryData body, IPrivateEventRouter eventos, ICommandRouter comandos)
        where TEvento : class, IPrivateEvent
    {
        var evento = ServiceBusDeserializador.Deserializar<TEvento>(body);
        return ct => eventos.InvokeAsync(evento, ct);
    }

    // Comando espejo deliberado (MEF-ADR-0024 decision #8): RecibirDepuracion tambien maneja
    // DiaDepurado y Wolverine ejecutaria ambos handlers si se despachara como evento.
    private static Func<CancellationToken, Task> DiaDepuradoComoComando(
        BinaryData body, IPrivateEventRouter eventos, ICommandRouter comandos)
    {
        var dia = ServiceBusDeserializador.Deserializar<DiaDepurado>(body);
        var comando = new RepartirMarcaciones(dia.CodigoColaborador, dia.Fecha);
        return ct => comandos.InvokeAsync(comando, ct);
    }
}
