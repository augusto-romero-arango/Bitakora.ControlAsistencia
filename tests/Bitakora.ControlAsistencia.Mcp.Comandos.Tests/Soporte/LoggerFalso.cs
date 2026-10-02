using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

/// <summary>Fake manual (nunca NSubstitute) de <see cref="ILogger{T}"/> que conserva cada entrada emitida.</summary>
public sealed class LoggerFalso<T> : ILogger<T>
{
    public sealed record Entrada(LogLevel Nivel, string Mensaje, IReadOnlyDictionary<string, object?> Estado);

    public List<Entrada> Entradas { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var estado = new Dictionary<string, object?>();
        if (state is IEnumerable<KeyValuePair<string, object?>> pares)
            foreach (var par in pares)
                estado[par.Key] = par.Value;
        Entradas.Add(new Entrada(logLevel, formatter(state, exception), estado));
    }
}
