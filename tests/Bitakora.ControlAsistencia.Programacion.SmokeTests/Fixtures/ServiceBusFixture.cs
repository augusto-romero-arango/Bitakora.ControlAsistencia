using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

public class ServiceBusFixture : IAsyncLifetime
{
    private ServiceBusClient? _client;

    public bool IsConfigured { get; private set; }

    public ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

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

    public async Task<T> WaitForMessageAsync<T>(
        string topicName,
        string subscriptionName,
        Func<T, bool> match,
        TimeSpan timeout)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
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
                var deserialized = JsonSerializer.Deserialize<T>(received.Body.ToString(), options);
                if (deserialized is not null && match(deserialized))
                {
                    await receiver.CompleteMessageAsync(received);
                    return deserialized;
                }

                if (deserialized is not null)
                {
                    await receiver.CompleteMessageAsync(received);
                    throw new InvalidOperationException(
                        $"Llego mensaje de tipo {typeof(T).Name} pero no cumplio el predicado. " +
                        $"Contenido: {received.Body}");
                }
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

    public async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekDeadLetterMessagesAsync(
        string topicName,
        string subscriptionName,
        int maxMessages = 10)
    {
        var options = new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter };
        await using var receiver = _client!.CreateReceiver(topicName, subscriptionName, options);

        var messages = await receiver.PeekMessagesAsync(maxMessages);
        return messages;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
            await _client.DisposeAsync();
    }
}
