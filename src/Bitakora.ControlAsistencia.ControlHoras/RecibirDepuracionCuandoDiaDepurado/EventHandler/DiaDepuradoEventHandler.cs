using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.RecibirDepuracionCuandoDiaDepurado.EventHandler;

// Issue #425: recibe cada foto de DiaDepurado (PrivateEvents.ControlHoras) y la traduce a los
// tipos ricos propios de ControlHoras.DomainEvents antes de entregarla al aggregate DiaCalculado
// (CA-ADR-0029 decision #5: el Function App es el unico ensamblado que ve las tres islas, asi que
// el mapeo vive aqui). Sin comando espejo: se consume directo con IPrivateEventHandlerAsync
// (MEF-ADR-0024 decision #8). Ningun consumidor nuevo: no publica ningun evento (issue #425,
// "Consumidores: ninguno nuevo").
// MEF-ADR-0009: partial class para soportar clase Mensajes en archivo separado si se requiere.
public partial class DiaDepuradoEventHandler : IPrivateEventHandlerAsync<DiaDepurado>
{
    private readonly IEventStore _eventStore;

    public DiaDepuradoEventHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task HandleAsync(DiaDepurado @event, CancellationToken ct = default)
    {
        var streamId = DiaCalculadoAggregateRoot.ComputarStreamId(@event.CodigoColaborador, @event.Fecha);

        var evento = new DomainEvents.DepuracionDiaRecibida(
            streamId,
            @event.CodigoColaborador,
            @event.Fecha,
            MapearColaborador(@event.Colaborador),
            @event.NombreTurno,
            @event.Franjas.Select(MapearFranja).ToList(),
            @event.Marcaciones.Select(MapearMarcacion).ToList(),
            MapearHoras(@event.HorasDiscriminadas));

        var existe = await _eventStore.ExistsAsync<DiaCalculadoAggregateRoot>(streamId, ct);

        if (existe)
        {
            var dia = (await _eventStore.GetAggregateRootAsync<DiaCalculadoAggregateRoot>(streamId, ct))!;
            dia.RecibirDepuracion(evento);
        }
        else
        {
            var dia = DiaCalculadoAggregateRoot.Iniciar(evento);
            _eventStore.StartStream(dia);
        }
    }

    private static DomainEvents.ResumenColaborador? MapearColaborador(
        Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador? colaborador) =>
        colaborador is null
            ? null
            : new DomainEvents.ResumenColaborador(
                colaborador.Identificacion, colaborador.CodigoColaborador, colaborador.NombreCompleto);

    private static DomainEvents.FranjaDepurada MapearFranja(FranjaDepurada franja) =>
        new(franja.HoraInicioProgramada, franja.HoraFinProgramada, franja.DiaOffsetFin,
            franja.Entrada, franja.Salida, franja.EsAnomala);

    private static DomainEvents.MarcacionDelDia MapearMarcacion(MarcacionDelDia marcacion) =>
        new(marcacion.Timestamp, marcacion.Tipo);

    private static DomainEvents.HorasDiscriminadas MapearHoras(HorasDiscriminadas horas) =>
        new(horas.HorasPorConcepto, horas.Trazabilidad);
}
