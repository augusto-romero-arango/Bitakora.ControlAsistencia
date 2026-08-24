using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
// FranjaDepurada/MarcacionDelDia/HorasDiscriminadas existen homonimos en las dos islas: el nombre
// corto resuelve al tipo de bus (using de arriba) y el persistido va calificado como DomainEvents.X.
using ColaboradorBus = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.RecibirDepuracionCuandoDiaDepurado.EventHandler;

// Traduce cada foto de DiaDepurado (bus) a los tipos ricos de ControlHoras.DomainEvents antes de
// entregarla al aggregate DiaCalculado: el mapeo vive aqui porque el Function App es el unico
// ensamblado que ve las tres islas (CA-ADR-0029 decision #5). Se consume directo con
// IPrivateEventHandlerAsync, sin comando espejo (MEF-ADR-0024 decision #8), y no publica ningun
// evento.
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

    private static DomainEvents.ResumenColaborador? MapearColaborador(ColaboradorBus? colaborador) =>
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
