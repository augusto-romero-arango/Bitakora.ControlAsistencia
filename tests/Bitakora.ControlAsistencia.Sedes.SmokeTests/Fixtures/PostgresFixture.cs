using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Bitakora.ControlAsistencia.Sedes.SmokeTests.Fixtures;

public class PostgresFixture : IAsyncLifetime
{
    private string _connectionString = null!;
    private string _tenantId = null!;

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

        _tenantId = IdentidadDePrueba.Desde(configuration).TenantId;

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
    /// Issue #354: <see cref="ExisteEventoAsync"/> solo responde "hay al menos uno", asi que no
    /// distingue "quedo el evento de la primera request" de "la segunda request escribio otro".
    /// Un test que afirme que un rechazo NO agrego un evento necesita el conteo exacto. Sin
    /// polling a proposito: se usa despues de una respuesta sincrona de rechazo (409), cuando el
    /// escenario ya espero con <see cref="ExisteEventoAsync"/> a que apareciera el evento legitimo.
    /// </remarks>
    public async Task<int> ContarEventosAsync(string schema, string streamId, string tipoEvento) =>
        (await ObtenerEventosInternoAsync(schema, streamId, tipoEvento)).Count;

    /// <summary>
    /// Espera a que el daemon de proyecciones (MEF-ADR-0034 seccion 3) materialice un documento
    /// Marten -- lifecycle Async, efecto del worker, no del write-side inline. Filtra por tenant_id
    /// porque el named store del worker declara <c>Policies.AllDocumentsAreMultiTenanted()</c>
    /// (tenancy conjoined, CA-ADR-0027): sin ese filtro un documento de otro tenant con el mismo id
    /// daria un falso positivo.
    /// </summary>
    public Task<bool> ExisteDocumentoAsync(
        string schema, string tabla, string id, TimeSpan timeout,
        string? campoJson = null, string? valorJson = null)
    {
        return Polling.WaitUntilTrueAsync(async () =>
        {
            var data = await ObtenerDocumentoInternoAsync(schema, tabla, id);
            if (data is null)
                return false;

            if (campoJson is null || valorJson is null)
                return true;

            return data.Value.TryGetProperty(campoJson, out var prop) &&
                prop.ToString() == valorJson;
        }, timeout);
    }

    /// <summary>
    /// Obtiene el primer evento del tipo indicado en el stream, sin filtrar por contenido.
    /// </summary>
    /// <remarks>
    /// Issue #351: el filtro por (campoJson, valorJson) del overload de abajo compara
    /// <c>JsonElement.ToString()</c> contra un texto -- solo sirve para campos ESCALARES. Para un
    /// campo objeto (un VO serializado) esa comparacion nunca
    /// coincide: mt_events.data es jsonb, y PostgreSQL no preserva ni el whitespace ni el orden de
    /// las claves (docs 8.14.1), mientras que <c>ToString()</c> sobre un objeto devuelve el texto
    /// crudo tal como llego. El test que necesita verificar contenido de un campo objeto usa este
    /// overload y compara por VALOR, deserializando con las opciones reales de Marten.
    /// Es seguro no filtrar porque cada smoke test usa una identificacion nueva: su stream contiene
    /// un solo evento de cada tipo.
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
            FROM {EscaparIdentificador(schema)}.mt_events
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

    private async Task<JsonElement?> ObtenerDocumentoInternoAsync(string schema, string tabla, string id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT data
            FROM {EscaparIdentificador(schema)}.{EscaparIdentificador(tabla)}
            WHERE id = @id
              AND tenant_id = @tenantId
            """;
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("tenantId", _tenantId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var json = reader.GetString(0);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string EscaparIdentificador(string identificador)
    {
        // Solo permitir caracteres alfanumericos y guion bajo para prevenir SQL injection
        if (!System.Text.RegularExpressions.Regex.IsMatch(identificador, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            throw new ArgumentException($"Identificador SQL invalido: {identificador}");
        return identificador;
    }
}
