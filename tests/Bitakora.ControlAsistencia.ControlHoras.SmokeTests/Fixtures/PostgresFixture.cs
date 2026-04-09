using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

public class PostgresFixture : IAsyncLifetime
{
    private string _connectionString = null!;

    public async ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        _connectionString = configuration["Postgres:ConnectionString"]
            ?? throw new InvalidOperationException(
                "Postgres:ConnectionString no esta configurado. Usa appsettings.json, appsettings.local.json o la variable de entorno Postgres__ConnectionString.");

        // Verificar conectividad
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
    }

    public async Task<bool> ExisteEventoAsync(
        string schema, string streamId, string tipoEvento, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var delay = TimeSpan.FromSeconds(1);

        while (DateTime.UtcNow < deadline)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT COUNT(1)
                FROM {EscaparSchema(schema)}.mt_events
                WHERE stream_id = @streamId
                  AND type = @tipoEvento
                """;
            cmd.Parameters.AddWithValue("streamId", streamId);
            cmd.Parameters.AddWithValue("tipoEvento", tipoEvento);

            var count = (long)(await cmd.ExecuteScalarAsync())!;
            if (count > 0)
                return true;

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            await Task.Delay(remaining < delay ? remaining : delay);
            if (delay < TimeSpan.FromSeconds(5))
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 1.5);
        }

        return false;
    }

    public async Task<List<T>> ObtenerEventosAsync<T>(
        string schema, string streamId, TimeSpan timeout)
    {
        var result = await Polling.WaitUntilAsync(async () =>
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT data
                FROM {EscaparSchema(schema)}.mt_events
                WHERE stream_id = @streamId
                ORDER BY seq_id
                """;
            cmd.Parameters.AddWithValue("streamId", streamId);

            var eventos = new List<T>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var json = reader.GetString(0);
                var evento = JsonSerializer.Deserialize<T>(json);
                if (evento is not null)
                    eventos.Add(evento);
            }

            return eventos.Count > 0 ? eventos : null;
        }, timeout);

        return result;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string EscaparSchema(string schema)
    {
        // Solo permitir caracteres alfanumericos y guion bajo para prevenir SQL injection
        if (!System.Text.RegularExpressions.Regex.IsMatch(schema, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            throw new ArgumentException($"Nombre de schema invalido: {schema}");
        return schema;
    }
}
