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

        while (DateTime.UtcNow < deadline)
        {
            var result = await probe();
            if (result is not null)
                return result;

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            await Task.Delay(remaining < delay ? remaining : delay);

            // Backoff simple: incrementar 50% hasta max 5s
            if (delay < TimeSpan.FromSeconds(5))
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 1.5);
        }

        throw new TimeoutException(
            $"Polling agoto el timeout de {timeout.TotalSeconds}s sin obtener resultado.");
    }
}
