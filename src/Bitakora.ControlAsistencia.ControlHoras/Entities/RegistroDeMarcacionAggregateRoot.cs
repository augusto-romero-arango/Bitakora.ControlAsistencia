using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Identidad: CodigoColaborador + Timestamp crudo como stream ID determinista. Unicas
// responsabilidades: idempotencia por duplicado exacto y normalizacion del timestamp.
public partial class RegistroDeMarcacionAggregateRoot : AggregateRoot
{
    public string CodigoColaborador { get; private set; } = null!;
    public DateTime TimestampCrudo { get; private set; }
    public DateTime TimestampNormalizado { get; private set; }
    public string? TipoMarcacion { get; private set; }
    public string? DispositivoId { get; private set; }

    // CA-ADR-0031: el timestamp va en ISO 8601 basico (sin ':' propios) para que Split(':') siempre
    // devuelva 3 partes. Cambiar el prefijo o el separador invalidaria la identidad de todos los
    // streams ya escritos.
    // El Timestamp llega sin offset y se interpreta como hora local de Colombia (UTC-5, sin DST).
    private const string PrefijoStreamId = "rdm";
    private const char SeparadorStreamId = ':';

    public static string ComputarStreamId(string codigoColaborador, DateTime timestamp) =>
        $"{PrefijoStreamId}{SeparadorStreamId}{codigoColaborador}{SeparadorStreamId}{timestamp:yyyyMMddTHHmmss}";

    // Un codigo con el separador vuelve ambigua la composicion del stream ID: dos marcaciones
    // distintas podrian producir el mismo id y, como la idempotencia se decide por existencia del
    // stream, la legitima se descartaria como duplicado. El validator del borde invoca esta regla en
    // vez de repetir el literal; tambien protege a ControlDiarioAggregateRoot, que usa el mismo separador.
    public static bool EsComponenteValidoDeStreamId(string codigoColaborador) =>
        !codigoColaborador.Contains(SeparadorStreamId);

    // public: requerido para que TestStore.ApplyEvent lo encuentre via GetMethods()
    public void Apply(MarcacionRegistrada e)
    {
        CodigoColaborador = e.CodigoColaborador;
        TimestampNormalizado = e.TimestampNormalizado;
        TipoMarcacion = e.TipoMarcacion;
        DispositivoId = e.DispositivoId;
    }

    // El timestamp crudo queda en el aggregate; el evento publica solo el normalizado.
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

    public RegistroDeMarcacionCreado CrearRegistroDeMarcacionCreado() =>
        new(CodigoColaborador, TimestampNormalizado, TipoMarcacion, DispositivoId);
}
