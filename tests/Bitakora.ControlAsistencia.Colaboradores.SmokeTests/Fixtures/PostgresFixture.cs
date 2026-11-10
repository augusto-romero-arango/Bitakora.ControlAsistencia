using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;

public class PostgresFixture : IAsyncLifetime
{
    private string _connectionString = null!;

    public bool IsConfigured { get; private set; }

    public string? SkipReason { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration["Postgres:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            IsConfigured = false;
            SkipReason = "Postgres no configurado. Usa appsettings.local.json o variable Postgres__ConnectionString.";
            return;
        }

        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
        }
        catch (NpgsqlException ex) when (ex.InnerException is SocketException or TimeoutException)
        {
            IsConfigured = false;
            SkipReason = $"No se pudo conectar a Postgres. Verifica que tu IP este en el firewall de Azure (psql-asist-dev). Detalle: {ex.InnerException.Message}";
            return;
        }

        IsConfigured = true;
        _connectionString = connectionString;
    }

    public Task<bool> ExisteEventoAsync(
        string schema, string streamId, string tipoEvento, TimeSpan timeout,
        string? campoJson = null, string? valorJson = null)
    {
        return Polling.WaitUntilTrueAsync(async () =>
        {
            var eventos = await ObtenerEventosInternoAsync(schema, streamId, tipoEvento);

            if (campoJson is null || valorJson is null)
                return eventos.Count > 0;

            return eventos.Any(e =>
                e.TryGetProperty(campoJson, out var prop) &&
                prop.ToString() == valorJson);
        }, timeout);
    }

    /// <summary>
    /// Cuenta los eventos del tipo indicado ya presentes en el stream, sin esperar.
    /// </summary>
    /// <remarks>
    /// Para afirmar que un rechazo NO agrego un evento (<see cref="ExisteEventoAsync"/> solo dice "hay
    /// al menos uno"). Sin polling a proposito: va despues del rechazo sincrono (409), cuando el
    /// escenario ya espero con <see cref="ExisteEventoAsync"/> el evento legitimo.
    /// </remarks>
    public async Task<int> ContarEventosAsync(string schema, string streamId, string tipoEvento) =>
        (await ObtenerEventosInternoAsync(schema, streamId, tipoEvento)).Count;

    public async Task<long> ContarEventosDelStreamAsync(string schema, string streamId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT count(*) FROM {EscaparSchema(schema)}.mt_events WHERE stream_id = @streamId";
        cmd.Parameters.AddWithValue("streamId", streamId);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// Obtiene el primer evento del tipo indicado en el stream, sin filtrar por contenido.
    /// </summary>
    /// <remarks>
    /// El filtro (campoJson, valorJson) del overload de abajo compara <c>JsonElement.ToString()</c> y solo
    /// sirve para campos escalares: jsonb no preserva whitespace ni orden de claves (PostgreSQL docs 8.14.1),
    /// asi que un campo objeto (un VO) nunca coincide. Para esos se usa este overload y se compara por valor.
    /// No filtrar es seguro: cada smoke usa una identificacion nueva y su stream tiene un evento de cada tipo.
    /// </remarks>
    public Task<T> ObtenerEventoAsync<T>(
        string schema, string streamId, string tipoEvento, TimeSpan timeout) =>
        ObtenerPrimerEventoAsync<T>(schema, streamId, tipoEvento, campoJson: null, valorJson: null, timeout);

    public Task<T> ObtenerEventoAsync<T>(
        string schema, string streamId, string tipoEvento,
        string campoJson, string valorJson, TimeSpan timeout) =>
        ObtenerPrimerEventoAsync<T>(schema, streamId, tipoEvento, campoJson, valorJson, timeout);

    private async Task<T> ObtenerPrimerEventoAsync<T>(
        string schema, string streamId, string tipoEvento,
        string? campoJson, string? valorJson, TimeSpan timeout)
    {
        var json = await Polling.WaitUntilAsync(async () =>
        {
            var eventos = await ObtenerEventosInternoAsync(schema, streamId, tipoEvento);

            var match = campoJson is null || valorJson is null
                ? eventos.FirstOrDefault()
                : eventos.FirstOrDefault(e =>
                    e.TryGetProperty(campoJson, out var prop) &&
                    prop.ToString() == valorJson);

            if (match.ValueKind == JsonValueKind.Undefined)
                return null;

            return JsonSerializer.Serialize(match);
        }, timeout);

        return JsonSerializer.Deserialize<T>(json)!;
    }

    private async Task<List<JsonElement>> ObtenerEventosInternoAsync(
        string schema, string streamId, string tipoEvento)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT data
            FROM {EscaparSchema(schema)}.mt_events
            WHERE stream_id = @streamId
              AND type = @tipoEvento
            ORDER BY seq_id
            """;
        cmd.Parameters.AddWithValue("streamId", streamId);
        cmd.Parameters.AddWithValue("tipoEvento", tipoEvento);

        var eventos = new List<JsonElement>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var json = reader.GetString(0);
            var elemento = JsonSerializer.Deserialize<JsonElement>(json);
            eventos.Add(elemento);
        }

        return eventos;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string EscaparSchema(string schema)
    {
        // Previene SQL injection: el schema va interpolado en el SQL, no parametrizado.
        if (!System.Text.RegularExpressions.Regex.IsMatch(schema, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            throw new ArgumentException($"Nombre de schema invalido: {schema}");
        return schema;
    }
}
