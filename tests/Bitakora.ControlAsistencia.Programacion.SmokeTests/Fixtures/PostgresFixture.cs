using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

// Issue #335: gemelo del PostgresFixture de ControlHoras.SmokeTests. Programacion tambien persiste
// en el event store (CrearTurnoCommandHandler -> IEventStore.StartStream), asi que sus smoke tests
// necesitan verificar ese efecto secundario y no solo el status code (MEF-ADR-0013, "Alcance de un
// smoke test: cobertura completa de efectos secundarios"). turno_creado no cruza ningun bus: la
// unica verificacion black-box posible de su contenido es leer mt_events.
public class PostgresFixture : IAsyncLifetime
{
    private string _connectionString = null!;

    public bool IsConfigured { get; private set; }

    public string? SkipReason { get; private set; }

    public string TenantId { get; private set; } = "tenant-smoke";

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

        TenantId = IdentidadDePrueba.Desde(configuration).TenantId;
        await LimpiarJornadaPredeterminadaAsync(SchemaProgramacion, TenantId);
    }

    public const string SchemaProgramacion = "programacion";

    public static string StreamIdPreferencias(string tenantId) => $"pp:{tenantId}";

    // Cada corrida debe ejercer la materializacion real, asi que se borran los
    // streams de TODAS las Jornadas que alguna vez fueron predeterminadas (historial de pp:{tenant}),
    // sus documentos en la vista y el stream de Preferencias. Acotado por tenant_id.
    public async Task LimpiarJornadaPredeterminadaAsync(string schema, string tenantId)
    {
        var streamPreferencias = StreamIdPreferencias(tenantId);
        var aBorrar = new List<string> { streamPreferencias };

        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT to_regclass('{EscaparSchema(schema)}.mt_events') IS NOT NULL
                """;
            if (!(bool)(await cmd.ExecuteScalarAsync())!)
                return;
        }

        var historial = await LeerEventosDeStreamAsync(schema, tenantId, streamPreferencias);
        foreach (var data in historial)
        {
            if (data.TryGetProperty("JornadaId", out var id) || data.TryGetProperty("jornadaId", out id))
                aBorrar.Add(id.ToString());
        }

        await BorrarStreamsAsync(schema, tenantId, aBorrar);
    }

    // Borra streams (eventos + registro del stream) y sus documentos en la vista LimitesDeJornada,
    // siempre acotado por tenant_id.
    public async Task BorrarStreamsAsync(string schema, string tenantId, IReadOnlyCollection<string> streamIds)
    {
        var ids = streamIds.Distinct().ToArray();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"""
                DELETE FROM {EscaparSchema(schema)}.mt_events WHERE tenant_id = @tenant AND stream_id = ANY(@ids);
                DELETE FROM {EscaparSchema(schema)}.mt_streams WHERE tenant_id = @tenant AND id = ANY(@ids);
                """;
            cmd.Parameters.AddWithValue("tenant", tenantId);
            cmd.Parameters.AddWithValue("ids", ids);
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var existe = conn.CreateCommand())
        {
            existe.CommandText = $"SELECT to_regclass('{EscaparSchema(schema)}.mt_doc_limitesdejornada') IS NOT NULL";
            if (!(bool)(await existe.ExecuteScalarAsync())!)
                return;
        }

        await using var docs = conn.CreateCommand();
        docs.CommandText = $"""
            DELETE FROM {EscaparSchema(schema)}.mt_doc_limitesdejornada
            WHERE tenant_id = @tenant AND id::text = ANY(@ids)
            """;
        docs.Parameters.AddWithValue("tenant", tenantId);
        docs.Parameters.AddWithValue("ids", ids);
        await docs.ExecuteNonQueryAsync();
    }

    // Cuenta TODOS los eventos del stream (cualquier tipo), acotado por tenant: sirve para demostrar
    // que una segunda lectura no escribio nada.
    public async Task<int> ContarEventosDeStreamAsync(string schema, string tenantId, string streamId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT count(*) FROM {EscaparSchema(schema)}.mt_events
            WHERE tenant_id = @tenant AND stream_id = @streamId
            """;
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("streamId", streamId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<List<JsonElement>> LeerEventosDeStreamAsync(string schema, string tenantId, string streamId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT data FROM {EscaparSchema(schema)}.mt_events
            WHERE tenant_id = @tenant AND stream_id = @streamId
            ORDER BY seq_id
            """;
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("streamId", streamId);

        var eventos = new List<JsonElement>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            eventos.Add(JsonSerializer.Deserialize<JsonElement>(reader.GetString(0)));
        return eventos;
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

    // Issue #665 CA-3: cuenta exacta, sin polling -- verifica que un no-op (retirar dos veces) no
    // agrego un evento adicional al stream, algo que ExisteEventoAsync (>= 1) no distingue.
    public async Task<int> ContarEventosAsync(string schema, string streamId, string tipoEvento)
    {
        var eventos = await ObtenerEventosInternoAsync(schema, streamId, tipoEvento);
        return eventos.Count;
    }

    public async Task<T> ObtenerEventoAsync<T>(
        string schema, string streamId, string tipoEvento,
        string campoJson, string valorJson, TimeSpan timeout)
    {
        var json = await Polling.WaitUntilAsync(async () =>
        {
            var eventos = await ObtenerEventosInternoAsync(schema, streamId, tipoEvento);

            var match = eventos.FirstOrDefault(e =>
                e.TryGetProperty(campoJson, out var prop) &&
                prop.ToString() == valorJson);

            if (match.ValueKind == JsonValueKind.Undefined)
                return null;

            return JsonSerializer.Serialize(match);
        }, timeout);

        return JsonSerializer.Deserialize<T>(json)!;
    }

    // Issue #700 CA-4: lee una clave de la metadata del evento (mt_events.headers, HeadersEnabled del
    // write-side), donde UnitOfWorkMiddleware de Cosmos 3.x estampa la identidad de tenancy.
    public Task<string> ObtenerHeaderDeEventoAsync(
        string schema, string streamId, string tipoEvento, string header, TimeSpan timeout)
    {
        return Polling.WaitUntilAsync(async () =>
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT headers ->> @header
                FROM {EscaparSchema(schema)}.mt_events
                WHERE stream_id = @streamId
                  AND type = @tipoEvento
                ORDER BY seq_id
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("header", header);
            cmd.Parameters.AddWithValue("streamId", streamId);
            cmd.Parameters.AddWithValue("tipoEvento", tipoEvento);

            return await cmd.ExecuteScalarAsync() as string;
        }, timeout);
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
        // Solo permitir caracteres alfanumericos y guion bajo para prevenir SQL injection
        if (!Regex.IsMatch(schema, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            throw new ArgumentException($"Nombre de schema invalido: {schema}");
        return schema;
    }
}
