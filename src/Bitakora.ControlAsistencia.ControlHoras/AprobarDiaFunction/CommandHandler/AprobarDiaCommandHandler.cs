using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction.CommandHandler;

// CA-ADR-0030: el aggregate declina con resultado y este handler traduce la razon a
// InvalidOperationException (-> 409, MEF-ADR-0004 capa 2). Aval del vacio (CA-7): un stream
// inexistente tambien es un acto valido, no un 404 -- se crea con DiaAprobado como primer evento.
// partial: la clase Mensajes vive en archivo separado (MEF-ADR-0009).
public partial class AprobarDiaCommandHandler : ICommandHandlerAsync<AprobarDia>
{
    private readonly IEventStore _eventStore;

    public AprobarDiaCommandHandler(IEventStore eventStore) => _eventStore = eventStore;

    public async Task HandleAsync(AprobarDia command, CancellationToken ct = default)
    {
        var streamId = DiaCalculadoAggregateRoot.ComputarStreamId(command.CodigoColaborador, command.Fecha);
        var existe = await _eventStore.ExistsAsync<DiaCalculadoAggregateRoot>(streamId, ct);

        var dia = existe
            ? (await _eventStore.GetAggregateRootAsync<DiaCalculadoAggregateRoot>(streamId, ct))!
            : new DiaCalculadoAggregateRoot();

        var resultado = dia.Aprobar(streamId, command.CodigoColaborador, command.Fecha, command.Decisiones);

        if (resultado != ResultadoAprobacion.Aprobado)
            throw new InvalidOperationException(MensajesPorResultado.Value[resultado]);

        if (!existe)
            _eventStore.StartStream(dia);
    }

    // Lazy y no un Dictionary de inicializacion directa: C# no garantiza el orden entre
    // inicializadores estaticos de distintos archivos de una misma partial class, y leer Mensajes.X
    // en el cctor corria antes de que ResourceManager (otra parte de esta clase) existiera.
    private static readonly Lazy<IReadOnlyDictionary<ResultadoAprobacion, string>> MensajesPorResultado = new(() =>
        new Dictionary<ResultadoAprobacion, string>
        {
            [ResultadoAprobacion.ConflictosSinDecidir] = Mensajes.ConflictosSinDecidir,
            [ResultadoAprobacion.CodigoSedeNoCandidata] = Mensajes.CodigoSedeNoCandidata,
            [ResultadoAprobacion.DecisionParaFranjaInvalida] = Mensajes.DecisionParaFranjaInvalida,
            [ResultadoAprobacion.DiaYaAprobado] = Mensajes.DiaYaAprobado,
        });
}
