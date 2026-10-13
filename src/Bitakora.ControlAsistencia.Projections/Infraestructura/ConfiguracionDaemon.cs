using Marten;

namespace Bitakora.ControlAsistencia.Projections.Infraestructura;

public static class ConfiguracionDaemon
{
    private static readonly TimeSpan TiempoMaximoDeHuecoEstancado = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Fija Projections.SkipStaleGapsDespiteLiveTransactionsAfter (null por defecto = nunca salta).
    ///
    /// Causa: los 4 stores comparten base y proceso; Marten cuenta las transacciones abiertas por
    /// base (no por schema) y cada lider HotCold mantiene una abierta (advisory lock transaccional).
    /// Cada store ve los shards de los otros como "posibles escritores vivos" y un hueco legitimo
    /// de secuencia (las secuencias de PostgreSQL no son transaccionales) detiene el store.
    ///
    /// Trade-off: se acepta perder un evento solo si un append mantiene su transaccion abierta mas
    /// de 5 min y luego confirma dentro del rango saltado (los appends del BC son cortos), a cambio
    /// de progreso garantizado (atraso maximo ~5 min ante un hueco).
    ///
    /// Descartado idle_in_transaction_session_timeout como backstop: mataria las sesiones de
    /// liderazgo HotCold, que estan idle in transaction a proposito.
    /// </summary>
    public static void TolerarHuecosEstancados(this StoreOptions opts)
        => opts.Projections.SkipStaleGapsDespiteLiveTransactionsAfter = TiempoMaximoDeHuecoEstancado;
}
