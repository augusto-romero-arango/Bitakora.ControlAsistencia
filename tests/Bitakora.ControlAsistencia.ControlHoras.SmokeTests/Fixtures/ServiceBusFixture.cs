using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

public class ServiceBusFixture : IAsyncLifetime
{
    private ServiceBusClient? _client;

    public ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration["ServiceBus:ConnectionString"]
            ?? throw new InvalidOperationException(
                "ServiceBus:ConnectionString no esta configurado. Usa appsettings.json, appsettings.local.json o la variable de entorno ServiceBus__ConnectionString.");

        _client = new ServiceBusClient(connectionString);

        return ValueTask.CompletedTask;
    }

    public async Task PublishAsync<T>(string topicName, T message, string? correlationId = null)
    {
        await using var sender = _client!.CreateSender(topicName);

        var json = JsonSerializer.Serialize(message);
        var sbMessage = new ServiceBusMessage(json)
        {
            ContentType = "application/json"
        };

        if (correlationId is not null)
            sbMessage.CorrelationId = correlationId;

        await sender.SendMessageAsync(sbMessage);
    }

    public async Task<T?> WaitForMessageAsync<T>(
        string topicName,
        string subscriptionName,
        string correlationId,
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

            if (received.CorrelationId == correlationId)
            {
                await receiver.CompleteMessageAsync(received);
                return JsonSerializer.Deserialize<T>(received.Body.ToString());
            }

            // Mensaje de otro test, abandonar para que vuelva a la cola
            await receiver.AbandonMessageAsync(received);
        }

        return default;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
            await _client.DisposeAsync();
    }
}
