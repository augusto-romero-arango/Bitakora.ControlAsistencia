using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
// FranjaDepurada/MarcacionDelDia/HorasDiscriminadas existen homonimos en las dos islas: el nombre
// corto resuelve al tipo de bus (using de arriba) y el persistido va calificado como DomainEvents.X.
using ColaboradorBus = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.RecibirDepuracionCuandoDiaDepurado.EventHandler;

// El mapeo bus -> DomainEvents vive aqui porque el Function App es el unico ensamblado que ve las
// islas de eventos (CA-ADR-0029 decision #5).
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
            MapearHoras(@event.HorasDiscriminadas),
            @event.MotivoAusencia,
            @event.VersionControlDiario);

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
            franja.Entrada, franja.Salida, franja.EsAnomala,
            franja.CodigoSedeProgramada, franja.NombreSedeProgramada, franja.CentroDeCostosProgramado);

    private static DomainEvents.MarcacionDelDia MapearMarcacion(MarcacionDelDia marcacion) =>
        new(marcacion.Timestamp, marcacion.Tipo,
            marcacion.CodigoSede, marcacion.NombreSede, marcacion.CentroDeCostos, marcacion.Pertenencia);

    private static DomainEvents.HorasDiscriminadas MapearHoras(HorasDiscriminadas horas) =>
        new(horas.HorasPorConcepto, horas.Trazabilidad);
}
