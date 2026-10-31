namespace Bitakora.ControlAsistencia.Rendimiento.Fixtures;

// Copia del patron de Mcp.Asistente.SmokeTests/Fixtures/Polling.cs: la preparacion espera a que las
// proyecciones materialicen lo sembrado antes de arrancar el cronometro.
public static class Polling
{
    public static async Task<T> WaitUntilAsync<T>(
        Func<Task<T?>> probe,
        TimeSpan timeout,
        CancellationToken ct,
        TimeSpan? interval = null) where T : class
    {
        var delay = interval ?? TimeSpan.FromSeconds(1);
        var deadline = DateTime.UtcNow + timeout;
        Exception? lastException = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var result = await probe();
                if (result is not null)
                    return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            await Task.Delay(remaining < delay ? remaining : delay, ct);

            if (delay < TimeSpan.FromSeconds(5))
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 1.5);
        }

        throw new TimeoutException(
            $"Polling agoto el timeout de {timeout.TotalSeconds}s."
            + (lastException is null ? "" : $" Ultima excepcion: {lastException.Message}"),
            lastException);
    }
}
