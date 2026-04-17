using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.Eventos;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// HU-105: Aggregate root que representa el registro puntual de una marcacion
// Identidad: EmpleadoId + Timestamp crudo como stream ID determinista (CA-5)
// Unicas responsabilidades: idempotencia por duplicado exacto y normalizacion del timestamp
// ADR-0015: partial class para soportar clase Mensajes en archivo separado si se requiere
public partial class RegistroDeMarcacionAggregateRoot : AggregateRoot
{
    // CA-5: estado que refleja la marcacion persistida
    public string EmpleadoId { get; private set; } = null!;
    public DateTime TimestampCrudo { get; private set; }
    public DateTime TimestampNormalizado { get; private set; }
    public string? TipoMarcacion { get; private set; }
    public string? DispositivoId { get; private set; }

    // CA-5: stream ID determinista: "{EmpleadoId}:{Timestamp:yyyy-MM-ddTHH:mm:ss}"
    public static string ComputarStreamId(string empleadoId, DateTime timestamp) =>
        $"{empleadoId}:{timestamp:yyyy-MM-ddTHH:mm:ss}";

    // Apply: reconstruye el estado del aggregate desde MarcacionRegistrada
    // public: requerido para que TestStore.ApplyEvent lo encuentre via GetMethods()
    public void Apply(MarcacionRegistrada e)
    {
        EmpleadoId = e.EmpleadoId;
        TimestampNormalizado = e.TimestampNormalizado;
        TipoMarcacion = e.TipoMarcacion;
        DispositivoId = e.DispositivoId;
    }

    // Factory interno: crea el aggregate con el evento en _uncommittedEvents para StartStream
    // El streamId y el timestamp crudo quedan codificados en el aggregate; el evento emitido
    // publica solo el timestamp normalizado (CA-2).
    internal static RegistroDeMarcacionAggregateRoot Iniciar(
        string streamId, DateTime timestampCrudo, MarcacionRegistrada evento)
    {
        var registro = new RegistroDeMarcacionAggregateRoot
        {
            Id = streamId,
            TimestampCrudo = timestampCrudo
        };
        registro._uncommittedEvents.Add(evento);
        registro.Apply(evento);
        return registro;
    }
}
