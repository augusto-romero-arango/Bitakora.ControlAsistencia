using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.Sedes.SmokeTests.Fixtures;

public class ServiceBusFixture : IAsyncLifetime
{
    // Deben casar con las claves que TenantContextMiddleware lee de ApplicationProperties: "tenant-id"
    // (la de Wolverine para Envelope.TenantId) y TenancyHeaders.UserId/OrganizationMembershipId.
    private const string TenantIdApplicationProperty = "tenant-id";
    private const string UserIdApplicationProperty = "user_id";
    private const string OrganizationMembershipIdApplicationProperty = "organization_membership_id";

    private ServiceBusClient? _client;
    private JsonSerializerOptions _jsonOptions = null!;
    private IdentidadDePrueba _identidad = null!;

    public bool IsConfigured { get; private set; }

    public ValueTask InitializeAsync()
    {
        // PropertyNameCaseInsensitive: Wolverine publica en camelCase.
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

    public async Task PublishAsync<T>(string topicName, T message, string? correlationId = null)
    {
        await using var sender = _client!.CreateSender(topicName);

        var json = JsonSerializer.Serialize(message);
        var sbMessage = new ServiceBusMessage(json)
        {
            ContentType = "application/json"
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
                    return deserialized;
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

    private const int TamanoPaginaDeadLetter = 100;

    // Peek iterativo: una llamada no devuelve el DLQ entero (Microsoft Learn,
    // "message-browsing#maximum-number-of-messages"), y el peek no completa mensajes.
    public async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekDeadLetterMessagesAsync(
        string topicName,
        string subscriptionName)
    {
        var options = new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter };
        await using var receiver = _client!.CreateReceiver(topicName, subscriptionName, options);

        var mensajes = new List<ServiceBusReceivedMessage>();
        long? desdeSecuencia = null;

        while (true)
        {
            var pagina = await receiver.PeekMessagesAsync(
                maxMessages: TamanoPaginaDeadLetter, fromSequenceNumber: desdeSecuencia);
            if (pagina.Count == 0)
                break;

            mensajes.AddRange(pagina);
            desdeSecuencia = pagina[^1].SequenceNumber + 1;
        }

        return mensajes;
    }

    // Acota el assert a "hay un dead letter de ESTA corrida" en vez de "el DLQ esta globalmente
    // vacio". T es una forma minima plana que solo declara el identificador de correlacion, sin
    // depender de la deserializacion custom de los value objects ricos del contrato.
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

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
            await _client.DisposeAsync();
    }
}
