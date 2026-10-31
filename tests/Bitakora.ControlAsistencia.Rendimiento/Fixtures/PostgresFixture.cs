using System.Net.Sockets;
using Bitakora.ControlAsistencia.Rendimiento.Reporte;
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

    /// <summary>Reloj de la base: el origen de las fases, que se miden con los timestamps que pone la base.</summary>
    public async Task<DateTimeOffset> AhoraAsync(CancellationToken ct)
    {
        await using var comando = _fuente.CreateCommand("SELECT clock_timestamp()");
        return new DateTimeOffset((DateTime)(await comando.ExecuteScalarAsync(ct))!, TimeSpan.Zero);
    }

    /// <summary>
    /// Cuando ocurrio cada eslabon de la cadena de programacion para los colaboradores cuyo codigo
    /// empieza por <paramref name="prefijoCodigo"/>, en segundos desde <paramref name="origen"/>:
    /// la solicitud confirmada en Programacion, el turno asignado en ControlDiario, la depuracion
    /// recibida en DiaCalculado y la fila de AsistenciaDiaria. Los nombres de evento son los alias
    /// de Marten y los ids de stream los de ControlDiarioAggregateRoot ("cd:") y
    /// DiaCalculadoAggregateRoot ("dc:"); el JSON de los eventos va en PascalCase (Casing.Default).
    /// </summary>
    public async Task<IReadOnlyList<FaseDeLaCorrida>> LeerFasesAsync(
        string tenant, string prefijoCodigo, DateTimeOffset origen, CancellationToken ct)
    {
        // El filtro por timestamp solo acota el barrido de mt_events; el prefijo de la corrida es
        // el que identifica las filas. Un minuto de holgura cubre la diferencia de relojes.
        await using var comando = _fuente.CreateCommand(
            """
            WITH tiempos AS (
                SELECT 1 AS orden, 'Programacion: solicitud confirmada' AS fase, timestamp AS t
                FROM programacion.mt_events
                WHERE tenant_id = $1 AND type = 'programacion_turno_solicitada'
                  AND timestamp >= $2 - interval '1 minute'
                  AND data -> 'Colaborador' ->> 'CodigoColaborador' LIKE $3
                UNION ALL
                SELECT 2, 'ControlHoras: turno asignado', timestamp
                FROM control_horas.mt_events
                WHERE tenant_id = $1 AND type = 'turno_diario_asignado'
                  AND timestamp >= $2 - interval '1 minute' AND stream_id LIKE $4
                UNION ALL
                SELECT 3, 'ControlHoras: depuracion recibida', timestamp
                FROM control_horas.mt_events
                WHERE tenant_id = $1 AND type = 'depuracion_dia_recibida'
                  AND timestamp >= $2 - interval '1 minute' AND stream_id LIKE $5
                UNION ALL
                SELECT 4, 'Proyeccion: fila de AsistenciaDiaria', mt_last_modified
                FROM control_horas.mt_doc_asistenciadiaria
                WHERE tenant_id = $1 AND id LIKE $5
            )
            SELECT fase, count(*),
                   extract(epoch FROM min(t) - $2)::float8,
                   percentile_cont(0.5) WITHIN GROUP (ORDER BY extract(epoch FROM t - $2)::float8),
                   percentile_cont(0.9) WITHIN GROUP (ORDER BY extract(epoch FROM t - $2)::float8),
                   extract(epoch FROM max(t) - $2)::float8
            FROM tiempos
            GROUP BY orden, fase
            ORDER BY orden
            """);
        comando.CommandTimeout = 120;
        comando.Parameters.AddWithValue(tenant);
        comando.Parameters.AddWithValue(origen);
        comando.Parameters.AddWithValue($"{prefijoCodigo}%");
        comando.Parameters.AddWithValue($"cd:{prefijoCodigo}%");
        comando.Parameters.AddWithValue($"dc:{prefijoCodigo}%");
        await using var lector = await comando.ExecuteReaderAsync(ct);

        var fases = new List<FaseDeLaCorrida>();
        while (await lector.ReadAsync(ct))
            fases.Add(new FaseDeLaCorrida(
                lector.GetString(0), lector.GetInt64(1),
                lector.GetDouble(2), lector.GetDouble(3), lector.GetDouble(4), lector.GetDouble(5)));

        return fases;
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
