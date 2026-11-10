using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction.CommandHandler;

// Aval del vacio: aprobar un dia sin stream es un acto valido, no un 404; el stream se crea con
// DiaAprobado como primer evento.
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
            throw new ReglaDeNegocioDeclinadaException(MensajesPorResultado.Value[resultado]);

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
            [ResultadoAprobacion.MarcacionesAmbiguasSinResolver] = Mensajes.MarcacionesAmbiguasSinResolver,
        });
}
