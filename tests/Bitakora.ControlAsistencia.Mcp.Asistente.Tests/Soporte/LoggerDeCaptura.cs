using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

public sealed record RegistroCapturado(
    LogLevel Nivel, EventId EventId, IReadOnlyDictionary<string, object?> Propiedades, Exception? Excepcion);

/// <summary>ILogger fake manual que conserva las propiedades con nombre de cada log estructurado.</summary>
public sealed class LoggerDeCaptura<T> : ILogger<T>
{
    private readonly List<RegistroCapturado> _registros = [];

    public IReadOnlyList<RegistroCapturado> Registros
    {
        get { lock (_registros) return [.. _registros]; }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var propiedades = state is IEnumerable<KeyValuePair<string, object?>> pares
            ? pares.ToDictionary(p => p.Key, p => p.Value)
            : [];
        lock (_registros)
            _registros.Add(new RegistroCapturado(logLevel, eventId, propiedades, exception));
    }
}

/// <summary>TimeProvider fake manual cuyo "ahora" solo avanza cuando el test lo indica.</summary>
public sealed class RelojAvanzable : TimeProvider
{
    private long _ticks;

    public override long TimestampFrequency => 1000;

    public override long GetTimestamp() => Interlocked.Read(ref _ticks);

    public void Avanzar(TimeSpan duracion) => Interlocked.Add(ref _ticks, (long)duracion.TotalMilliseconds);
}
