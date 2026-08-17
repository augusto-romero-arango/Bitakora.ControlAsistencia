using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// HU-105: Aggregate root que representa el registro puntual de una marcacion
// Identidad: CodigoColaborador + Timestamp crudo como stream ID determinista (CA-5)
// Unicas responsabilidades: idempotencia por duplicado exacto y normalizacion del timestamp
// ADR-0015: partial class para soportar clase Mensajes en archivo separado si se requiere
public partial class RegistroDeMarcacionAggregateRoot : AggregateRoot
{
    // CA-5: estado que refleja la marcacion persistida
    public string CodigoColaborador { get; private set; } = null!;
    public DateTime TimestampCrudo { get; private set; }
    public DateTime TimestampNormalizado { get; private set; }
    public string? TipoMarcacion { get; private set; }
    public string? DispositivoId { get; private set; }

    // CA-5: stream ID determinista: "{CodigoColaborador}:{Timestamp:yyyy-MM-ddTHH:mm:ss}"
    // Cambiar el separador invalidaria la identidad de todos los streams ya escritos, por eso vive
    // como constante privada: nadie lo compone a mano desde afuera.
    private const char SeparadorStreamId = ':';

    public static string ComputarStreamId(string codigoColaborador, DateTime timestamp) =>
        $"{codigoColaborador}{SeparadorStreamId}{timestamp:yyyy-MM-ddTHH:mm:ss}";

    // Issue #279 CA-3: un CodigoColaborador que contenga el separador vuelve ambigua la composicion del
    // stream ID -- dos combinaciones distintas de colaborador y timestamp pueden producir el mismo id, y
    // como la idempotencia se decide por existencia del stream, una marcacion legitima se descartaria
    // como "duplicado exacto". La regla vive junto al formato que la origina (MEF-ADR-0012,
    // Tell-don't-Ask): el validator del borde la invoca en vez de repetir el literal del separador.
    // ControlDiarioAggregateRoot compone su stream ID con el mismo separador, asi que rechazarlo en el
    // borde protege ambos streams; unificar el separador entre ambos aggregates queda pendiente.
    public static bool EsComponenteValidoDeStreamId(string codigoColaborador) =>
        !codigoColaborador.Contains(SeparadorStreamId);

    // Apply: reconstruye el estado del aggregate desde MarcacionRegistrada
    // public: requerido para que TestStore.ApplyEvent lo encuentre via GetMethods()
    public void Apply(MarcacionRegistrada e)
    {
        CodigoColaborador = e.CodigoColaborador;
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

    // Issue #270 CA-4: traductor del evento de dominio persistido (MarcacionRegistrada) al contrato
    // de bus (RegistroDeMarcacionCreado). Tell-don't-Ask: el aggregate es duenio del estado y entrega
    // el contrato ya empaquetado al handler -- espejo exacto de
    // ControlDiarioAggregateRoot.CrearDiaCalculado(). El handler no construye el contrato campo por campo.
    public RegistroDeMarcacionCreado CrearRegistroDeMarcacionCreado() =>
        new(CodigoColaborador, TimestampNormalizado, TipoMarcacion, DispositivoId);
}
