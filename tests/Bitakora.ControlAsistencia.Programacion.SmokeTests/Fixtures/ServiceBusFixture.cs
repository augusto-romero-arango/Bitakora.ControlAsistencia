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

    public async Task<T?> WaitForMessageAsync<T>(
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
            }
            catch (JsonException)
            {
                // Mensaje con formato incompatible, ignorar
            }

            // Mensaje de otro test o formato distinto, abandonar para que vuelva a la cola
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
