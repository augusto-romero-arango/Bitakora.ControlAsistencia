using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

public class ServiceBusFixture : IAsyncLifetime
{
    // Issues #538 y #698: claves de wire verificadas por decompilacion (ilspycmd) de los ensamblados vigentes
    // -- Wolverine 6.16.0 (EnvelopeMapper<> mapea Envelope.TenantId al header "tenant-id" sin prefijo,
    // y AzureServiceBusEnvelopeMapper copia cada ApplicationProperty a Envelope.Headers tal cual) y
    // Cosmos.MultiTenancy.CritterStack 2.3.0 (WolverineMessageContextTenantResolver lee UserId de
    // Envelope.Headers["user_id"]). Issue #698 suma "organization_membership_id"
    // (TenancyHeaders.OrganizationMembershipId de Cosmos 3.x), que viaja por el mismo camino que
    // "user_id" y Cosmos 2.3.1 ignora. Son otro plano que los headers HTTP X-Tenant-Id/X-User-Id/
    // X-Organization-Membership-Id de MEF-ADR-0028: viajan en ApplicationProperties del mensaje, no en la request.
    //
    // Advertencia para el flip a etapa (b): hoy los dominios NO montan un listener de Wolverine. Cada
    // evento entra por [ServiceBusTrigger] de Azure Functions y se despacha con IPrivateEventRouter,
    // que arma DeliveryOptions desde el ITenantContext del proceso -- nadie lee estas claves del
    // mensaje entrante, asi que la metadata queda declarada pero inerte. Sirve para no romper la
    // ventana el dia que exista ese puente; verificarlo al correr /install-auth.
    private const string TenantIdApplicationProperty = "tenant-id";
    private const string UserIdApplicationProperty = "user_id";
    private const string OrganizationMembershipIdApplicationProperty = "organization_membership_id";

    // Wolverine publica con Subject = Type.FullName del evento y el fan-in de #1175 despacha por ese valor.
    private static readonly IReadOnlyDictionary<string, string> SubjectPorTopic = new Dictionary<string, string>
    {
        ["programacion-turno-diario-solicitada"] = typeof(PrivateEvents.Programacion.ProgramacionTurnoDiarioSolicitada).FullName!,
        ["cancelacion-turno-diario-solicitada"] = typeof(PrivateEvents.Programacion.CancelacionTurnoDiarioSolicitada).FullName!,
        ["ausencia-diaria-programada"] = typeof(PrivateEvents.Programacion.AusenciaDiariaProgramada).FullName!,
        ["ausencia-diaria-cancelada"] = typeof(PrivateEvents.Programacion.AusenciaDiariaCancelada).FullName!,
        ["dia-depurado"] = typeof(PrivateEvents.ControlHoras.DiaDepurado).FullName!
    };

    private ServiceBusClient? _client;
    private JsonSerializerOptions _jsonOptions = null!;
    private IdentidadDePrueba _identidad = null!;

    public bool IsConfigured { get; private set; }

    public ValueTask InitializeAsync()
    {
        // Issue #183: el payload de DiaDepurado es ahora 100% primitivo (HorasDiscriminadas), asi que
        // se deserializa con el serializador POR DEFECTO -- SIN registrar ConfigurarSerializacion de
        // ningun VO rico. Esto demuestra la cura: el consumidor (incluido otro stack) ya no depende de
        // nuestra serializacion interna de Marten. Si alguien reintroduce un tipo rico al payload, este
        // smoke fallaria al deserializar (NotSupportedException) -- barrera de regresion end-to-end.
        // PropertyNameCaseInsensitive: Wolverine publica en camelCase (ver regression test del issue #29).
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        _identidad = IdentidadDePrueba.Desde(configuration);

        var connectionString = configuration["ServiceBus:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            IsConfigured = false;
            return ValueTask.CompletedTask;
        }

        IsConfigured = true;
        _client = new ServiceBusClient(connectionString);

        return ValueTask.CompletedTask;
    }

    public async Task PurgeAsync(string topicName, string subscriptionName)
    {
        await using var receiver = _client!.CreateReceiver(topicName, subscriptionName);
        var maxWait = TimeSpan.FromSeconds(2);

        while (true)
        {
            var message = await receiver.ReceiveMessageAsync(maxWait);
            if (message is null)
                break;

            await receiver.CompleteMessageAsync(message);
        }
    }

    public async Task PublishAsync<T>(string codigoColaborador, string topicName, T message, string? correlationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoColaborador);
        if (!SubjectPorTopic.TryGetValue(topicName, out var subject))
            throw new ArgumentException(
                $"El topic '{topicName}' no esta en el mapa de Subject del fan-in; agregalo a SubjectPorTopic.",
                nameof(topicName));

        await using var sender = _client!.CreateSender(topicName);

        var json = JsonSerializer.Serialize(message);
        var sbMessage = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            SessionId = codigoColaborador,
            Subject = subject
        };
        sbMessage.ApplicationProperties[TenantIdApplicationProperty] = _identidad.TenantId;
        sbMessage.ApplicationProperties[UserIdApplicationProperty] = _identidad.UserId;
        sbMessage.ApplicationProperties[OrganizationMembershipIdApplicationProperty] = _identidad.OrganizationMembershipId;

        if (correlationId is not null)
            sbMessage.CorrelationId = correlationId;

        await sender.SendMessageAsync(sbMessage);
    }

    public async Task<T> WaitForMessageAsync<T>(
        string topicName,
        string subscriptionName,
        Func<T, bool> match,
        TimeSpan timeout) =>
        (await WaitForReceivedMessageAsync(topicName, subscriptionName, match, timeout)).Mensaje;

    public async Task<(T Mensaje, string? SessionId)> WaitForMessageConSesionAsync<T>(
        string topicName,
        string subscriptionName,
        Func<T, bool> match,
        TimeSpan timeout)
    {
        var (mensaje, recibido) = await WaitForReceivedMessageAsync(topicName, subscriptionName, match, timeout);
        return (mensaje, recibido.SessionId);
    }

    private async Task<(T Mensaje, ServiceBusReceivedMessage Recibido)> WaitForReceivedMessageAsync<T>(
        string topicName,
        string subscriptionName,
        Func<T, bool> match,
        TimeSpan timeout)
    {
        await using var receiver = _client!.CreateReceiver(topicName, subscriptionName);

        var deadline = DateTime.UtcNow + timeout;
        var descartados = 0;

        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            var maxWait = remaining < TimeSpan.FromSeconds(5) ? remaining : TimeSpan.FromSeconds(5);
            var received = await receiver.ReceiveMessageAsync(maxWait);

            if (received is null)
                continue;

            // Las suscripciones smoke-tests son compartidas: el mensaje ajeno se completa (abandonarlo
            // lo devolveria en bucle hasta el dead letter) y se sigue esperando.
            await receiver.CompleteMessageAsync(received);

            try
            {
                var deserialized = JsonSerializer.Deserialize<T>(received.Body.ToString(), _jsonOptions);
                if (deserialized is not null && match(deserialized))
                    return (deserialized, received);
            }
            catch (JsonException)
            {
                // Un cuerpo que no deserializa como T tambien es un mensaje ajeno.
            }

            descartados++;
        }

        throw new TimeoutException(
            $"No se recibio ningun mensaje que cumpliera el predicado en la suscripcion {subscriptionName} " +
            $"del topic {topicName} en {timeout.TotalSeconds}s ({descartados} mensajes ajenos descartados)");
    }

    public const string QueueFanIn = "actualizar-control-diario";

    private const int TamanoPaginaDeadLetter = 100;

    // Issue #223 CA-1: recorre el DLQ completo con peek iterativo (patron oficial de Microsoft Learn,
    // "message-browsing#maximum-number-of-messages"), en vez del tope fijo anterior de 10 mensajes.
    // El peek no completa mensajes, asi que iterar el DLQ entero es no destructivo.
    public async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekDeadLetterMessagesAsync(
        string topicName,
        string subscriptionName)
    {
        await using var receiver = _client!.CreateReceiver(topicName, subscriptionName, OpcionesDeadLetter);
        return await PeekTodosAsync(receiver);
    }

    private static readonly ServiceBusReceiverOptions OpcionesDeadLetter = new() { SubQueue = SubQueue.DeadLetter };

    private static async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekTodosAsync(ServiceBusReceiver receiver)
    {
        var mensajes = new List<ServiceBusReceivedMessage>();
        long? desdeSecuencia = null;

        while (true)
        {
            var pagina = await receiver.PeekMessagesAsync(
                maxMessages: TamanoPaginaDeadLetter, fromSequenceNumber: desdeSecuencia);
            if (pagina.Count == 0)
                return mensajes;

            mensajes.AddRange(pagina);
            desdeSecuencia = pagina[^1].SequenceNumber + 1;
        }
    }

    // Issue #223 CA-2/CA-3: acota el assert a "hay un dead letter de ESTA corrida" en vez de
    // "el DLQ esta globalmente vacio". T es una forma minima plana (ver DeadLetterMinimos.cs)
    // que solo declara el identificador de correlacion, sin depender de la deserializacion
    // custom de los value objects ricos del contrato (ADR-0025).
    public async Task<bool> ExisteDeadLetterDeEstaCorridaAsync<T>(
        string topicName,
        string subscriptionName,
        Func<T, bool> match)
    {
        var mensajes = await PeekDeadLetterMessagesAsync(topicName, subscriptionName);

        foreach (var mensaje in mensajes)
        {
            try
            {
                var deserializado = JsonSerializer.Deserialize<T>(mensaje.Body.ToString(), _jsonOptions);
                if (deserializado is not null && match(deserializado))
                    return true;
            }
            catch (JsonException)
            {
                continue;
            }
        }

        return false;
    }

    // El DLQ de una entidad con sesion se lee con un receptor normal, sin aceptar sesion. Filtrar por
    // SessionId detecta cualquier mensaje del colaborador sin depender de la forma del payload.
    public async Task<bool> ExisteDeadLetterEnQueueDeColaboradorAsync(string queueName, string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await using var receiver = _client!.CreateReceiver(queueName, OpcionesDeadLetter);
        var mensajes = await PeekTodosAsync(receiver);

        return mensajes.Any(m => m.SessionId == sessionId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
            await _client.DisposeAsync();
    }
}
