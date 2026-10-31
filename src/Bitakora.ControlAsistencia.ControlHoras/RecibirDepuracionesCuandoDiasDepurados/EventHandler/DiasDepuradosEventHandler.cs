using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
using ColaboradorBus = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.RecibirDepuracionesCuandoDiasDepurados.EventHandler;

// Mapeo duplicado a proposito de DiaDepuradoEventHandler (MEF-ADR-0018: dos ocurrencias; una se
// retira cuando la medicion decida entre el camino por dia y el de lote).
public partial class DiasDepuradosEventHandler : IPrivateEventHandlerAsync<DiasDepurados>
{
    private readonly IEventStore _eventStore;

    public DiasDepuradosEventHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task HandleAsync(DiasDepurados @event, CancellationToken ct = default)
    {
        foreach (var foto in @event.Dias)
        {
            var streamId = DiaCalculadoAggregateRoot.ComputarStreamId(foto.CodigoColaborador, foto.Fecha);

            var evento = new DomainEvents.DepuracionDiaRecibida(
                streamId,
                foto.CodigoColaborador,
                foto.Fecha,
                MapearColaborador(foto.Colaborador),
                foto.NombreTurno,
                foto.Franjas.Select(MapearFranja).ToList(),
                foto.Marcaciones.Select(MapearMarcacion).ToList(),
                MapearHoras(foto.HorasDiscriminadas),
                foto.MotivoAusencia);

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
