using System.Net.Sockets;
using Npgsql;

namespace Bitakora.ControlAsistencia.Rendimiento.Fixtures;

// Solo lectura (CA-ADR-0040): ninguna sentencia de esta fixture escribe. A diferencia de los smoke,
// no hace skip si no conecta: sin el conteo no hay medicion, asi que el benchmark falla con la causa.
public sealed class PostgresFixture : IAsyncLifetime
{
    private NpgsqlDataSource _fuente = null!;

    public async ValueTask InitializeAsync()
    {
        var connectionString = Configuracion.Obligatoria(
            "Postgres:ConnectionString",
            "Es la cadena Npgsql de psql-asist-dev (la misma de los smoke).");

        _fuente = NpgsqlDataSource.Create(connectionString);
        try
        {
            await using var conexion = await _fuente.OpenConnectionAsync();
        }
        catch (NpgsqlException ex) when (ex.InnerException is SocketException or TimeoutException)
        {
            throw new InvalidOperationException(
                $"No se pudo conectar a Postgres. Verifica que tu IP este en el firewall de Azure (psql-asist-dev). Detalle: {ex.InnerException.Message}",
                ex);
        }
    }

    /// <summary>
    /// Filas de AsistenciaDiaria (el ultimo eslabon de la cadena de programacion) de los
    /// colaboradores cuyo codigo empieza por <paramref name="prefijoCodigo"/>. El id del documento
    /// es "dc:{CodigoColaborador}:{yyyyMMdd}" (AsistenciaDiariaProjection).
    /// </summary>
    public async Task<long> ContarAsistenciasDiariasAsync(string tenant, string prefijoCodigo, CancellationToken ct)
    {
        await using var comando = _fuente.CreateCommand(
            "SELECT count(*) FROM control_horas.mt_doc_asistenciadiaria WHERE tenant_id = $1 AND id LIKE $2");
        comando.Parameters.AddWithValue(tenant);
        comando.Parameters.AddWithValue($"dc:{prefijoCodigo}%");

        return (long)(await comando.ExecuteScalarAsync(ct))!;
    }

    /// <summary>
    /// Conexiones a la base por estado (pg_stat_activity), para ver la presion de conexiones con
    /// mas resolucion que la metrica de plataforma (un punto por minuto). Incluye la conexion
    /// del propio muestreo, que siempre aparece como activa.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, long>> ContarConexionesPorEstadoAsync(CancellationToken ct)
    {
        await using var comando = _fuente.CreateCommand(
            "SELECT coalesce(state, ''), count(*) FROM pg_stat_activity WHERE datname = current_database() GROUP BY 1");
        await using var lector = await comando.ExecuteReaderAsync(ct);

        var conexiones = new Dictionary<string, long>
        {
            ["pg total"] = 0, ["pg activas"] = 0, ["pg inactivas"] = 0, ["pg en transaccion"] = 0
        };
        while (await lector.ReadAsync(ct))
        {
            var cantidad = lector.GetInt64(1);
            conexiones["pg total"] += cantidad;
            var columna = lector.GetString(0) switch
            {
                "active" => "pg activas",
                "idle" => "pg inactivas",
                "idle in transaction" or "idle in transaction (aborted)" => "pg en transaccion",
                _ => null
            };
            if (columna is not null)
                conexiones[columna] += cantidad;
        }

        return conexiones;
    }

    public async ValueTask DisposeAsync()
    {
        if (_fuente is not null)
            await _fuente.DisposeAsync();
    }
}
