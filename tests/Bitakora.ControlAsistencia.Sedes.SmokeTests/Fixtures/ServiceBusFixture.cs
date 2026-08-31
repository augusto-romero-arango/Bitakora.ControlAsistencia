using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.Sedes.SmokeTests.Fixtures;

public class ServiceBusFixture : IAsyncLifetime
{
    // Issue #538: claves de wire verificadas por decompilacion (ilspycmd) de los ensamblados vigentes
    // -- Wolverine.dll 6.16.0 (EnvelopeMapper<>: MapPropertyToHeader(x => x.TenantId, "tenant-id"),
    // sin prefijo) y Cosmos.MultiTenancy.CritterStack.dll 2.3.0 (WolverineMessageContextTenantResolver
    // lee UserId de envelope.Headers["user_id"]). NO son los headers HTTP X-Tenant-Id/X-User-Id de
    // MEF-ADR-0028: son planos distintos (ApplicationProperties del mensaje vs headers HTTP).
    private const string TenantIdApplicationProperty = "tenant-id";
    private const string UserIdApplicationProperty = "user_id";
    private const string TenantIdPorDefecto = "tenant-smoke";
    private const string UserIdPorDefecto = "smoke@bitakora.dev";

    private ServiceBusClient? _client;
    private JsonSerializerOptions _jsonOptions = null!;
    private string _tenantId = TenantIdPorDefecto;
    private string _userId = UserIdPorDefecto;

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

        _tenantId = configuration["Tenant:Id"] ?? TenantIdPorDefecto;
        _userId = configuration["Tenant:UserId"] ?? UserIdPorDefecto;

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
        sbMessage.ApplicationProperties[TenantIdApplicationProperty] = _tenantId;
        sbMessage.ApplicationProperties[UserIdApplicationProperty] = _userId;

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

        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            var maxWait = remaining < TimeSpan.FromSeconds(5) ? remaining : TimeSpan.FromSeconds(5);
            var received = await receiver.ReceiveMessageAsync(maxWait);

            if (received is null)
                continue;

            try
            {
                var deserialized = JsonSerializer.Deserialize<T>(received.Body.ToString(), _jsonOptions);
                if (deserialized is null)
                {
                    await receiver.CompleteMessageAsync(received);
                    continue;
                }

                if (match(deserialized))
                {
                    await receiver.CompleteMessageAsync(received);
                    return deserialized;
                }

                await receiver.CompleteMessageAsync(received);
                throw new InvalidOperationException(
                    $"Llego mensaje de tipo {typeof(T).Name} pero no cumplio el predicado. " +
                    $"Contenido: {received.Body}");
            }
            catch (JsonException)
            {
                await receiver.CompleteMessageAsync(received);
                continue;
            }
        }

        throw new TimeoutException(
            $"No se recibio ningun mensaje en la suscripcion {subscriptionName} " +
            $"del topic {topicName} en {timeout.TotalSeconds}s");
    }

    private const int TamanoPaginaDeadLetter = 100;

    // Recorre el DLQ completo con peek iterativo (patron oficial de Microsoft Learn,
    // "message-browsing#maximum-number-of-messages"). El peek no completa mensajes, asi que
    // iterar el DLQ entero es no destructivo.
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
