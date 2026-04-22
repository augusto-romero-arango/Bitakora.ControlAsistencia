using System.Text.Json.Serialization;

namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

// Issue #114: Detalle del retardo de una franja - intervalos retardados y compensados.
// Constructor privado + factory static (ADR-0015).
// [JsonConstructor] en ctor privado para que STJ pueda deserializar (patron PR 142).
public sealed partial record DetalleRetardo
{
    [JsonConstructor]
    private DetalleRetardo(
        IReadOnlyList<IntervaloTemporal> tiempoRetardado,
        IReadOnlyList<IntervaloTemporal> tiempoCompensado,
        int minutosRetardados,
        int minutosCompensados)
    {
        TiempoRetardado = tiempoRetardado;
        TiempoCompensado = tiempoCompensado;
        MinutosRetardados = minutosRetardados;
        MinutosCompensados = minutosCompensados;
    }

    public IReadOnlyList<IntervaloTemporal> TiempoRetardado { get; }
    public IReadOnlyList<IntervaloTemporal> TiempoCompensado { get; }
    public int MinutosRetardados { get; }
    public int MinutosCompensados { get; }

    // MinutosRetardados - MinutosCompensados (seguro por invariante del factory).
    public int RetardoNeto => MinutosRetardados - MinutosCompensados;

    // Factory con invariante: MinutosCompensados <= MinutosRetardados.
    public static DetalleRetardo Crear(
        IReadOnlyList<IntervaloTemporal> tiempoRetardado,
        IReadOnlyList<IntervaloTemporal> tiempoCompensado)
    {
        var minutosRetardados = tiempoRetardado.Sum(i => i.DuracionEnMinutos);
        var minutosCompensados = tiempoCompensado.Sum(i => i.DuracionEnMinutos);
        if (minutosCompensados > minutosRetardados)
            throw new ArgumentException(Mensajes.CompensadosExcedenRetardados);
        return new DetalleRetardo(tiempoRetardado, tiempoCompensado, minutosRetardados, minutosCompensados);
    }

    // Franja sin retardo ni compensacion. Instancia unica reusable (inmutable).
    public static readonly DetalleRetardo Vacio = new([], [], 0, 0);
}
