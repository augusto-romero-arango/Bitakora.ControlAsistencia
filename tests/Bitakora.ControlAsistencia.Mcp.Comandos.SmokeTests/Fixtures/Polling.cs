namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;

// Copia deliberada de Colaboradores.SmokeTests/Fixtures/Polling.cs (excepcion documentada,
// MEF-ADR-0048 seccion 6): la proyeccion del directorio de colaboradores (#590) es Async -- justo
// despues del arrange, solicitar_programacion_turno puede omitir al colaborador todavia no
// materializado. El assert vive DENTRO del polling: las llamadas que lo omiten no hacen ningun
// POST (sin efecto secundario en Programacion), la primera que lo encuentra programa y corta el
// loop.
public static class Polling
{
    public static async Task<T> WaitUntilAsync<T>(
        Func<Task<T?>> probe,
        TimeSpan timeout,
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
            catch (Exception ex)
            {
                lastException = ex;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            await Task.Delay(remaining < delay ? remaining : delay);

            // Backoff simple: incrementar 50% hasta max 5s
            if (delay < TimeSpan.FromSeconds(5))
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 1.5);
        }

        if (lastException is not null)
            throw new TimeoutException(
                $"Polling agoto el timeout de {timeout.TotalSeconds}s. Ultima excepcion: {lastException.Message}",
                lastException);

        throw new TimeoutException(
            $"Polling agoto el timeout de {timeout.TotalSeconds}s sin obtener resultado.");
    }
}
