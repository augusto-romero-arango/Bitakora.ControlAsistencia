namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

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

    public static async Task<bool> WaitUntilTrueAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        TimeSpan? interval = null)
    {
        var delay = interval ?? TimeSpan.FromSeconds(1);
        var deadline = DateTime.UtcNow + timeout;
        Exception? lastException = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (await condition())
                    return true;
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            await Task.Delay(remaining < delay ? remaining : delay);

            if (delay < TimeSpan.FromSeconds(5))
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 1.5);
        }

        if (lastException is not null)
            throw new TimeoutException(
                $"Polling agoto el timeout de {timeout.TotalSeconds}s. Ultima excepcion: {lastException.Message}",
                lastException);

        return false;
    }
}
