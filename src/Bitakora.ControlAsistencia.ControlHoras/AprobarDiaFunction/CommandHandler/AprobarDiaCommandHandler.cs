using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction.CommandHandler;

// Issue #489: handler del acto de aprobar. CA-ADR-0030: el aggregate declina con resultado (nunca
// lanza, nunca emite evento de fallo persistido); este handler traduce la razon del rechazo a
// InvalidOperationException (-> 409, MEF-ADR-0004 capa 2). Aval del vacio (CA-7): un stream
// inexistente tambien es un acto valido -- crea el stream con DiaAprobado como primer evento.
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

    // Lookup map (seleccion por clave discreta, ver "Lookup map sobre switch/if" del implementer):
    // cada valor de fallo de ResultadoAprobacion resuelve al mensaje .resx que le corresponde.
    // Lazy, no un Dictionary de inicializacion directa: un campo estatico plano evaluaria
    // Mensajes.X (que lee el campo estatico ResourceManager de la OTRA parte de esta partial
    // class, en AprobarDiaCommandHandler.Mensajes.cs) durante el cctor combinado del tipo, con
    // riesgo de correr antes de que ResourceManager se inicialice -- el orden entre inicializadores
    // estaticos de distintos archivos de una misma partial class no esta garantizado por C#. Lazy
    // difiere esa lectura al primer uso real, cuando el tipo ya termino de inicializarse por completo.
    private static readonly Lazy<IReadOnlyDictionary<ResultadoAprobacion, string>> MensajesPorResultado = new(() =>
        new Dictionary<ResultadoAprobacion, string>
        {
            [ResultadoAprobacion.ConflictosSinDecidir] = Mensajes.ConflictosSinDecidir,
            [ResultadoAprobacion.CodigoSedeNoCandidata] = Mensajes.CodigoSedeNoCandidata,
            [ResultadoAprobacion.DecisionParaFranjaInvalida] = Mensajes.DecisionParaFranjaInvalida,
            [ResultadoAprobacion.DiaYaAprobado] = Mensajes.DiaYaAprobado,
        });
}
