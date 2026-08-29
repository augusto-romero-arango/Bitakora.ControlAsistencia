using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.PrivateEvents.Sedes;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;

// Issue #467: reaccion del dueno del dato (MEF-ADR-0046 paso 2) que resuelve la sede de una
// marcacion contra el read-side propio de Sedes. Mismo evento de entrada que consume ControlHoras
// (PrivateEvents.ControlHoras.RegistroDeMarcacionCreado) -- un topic, dos consumidores, cada uno con
// su propia subscription (MEF-ADR-0001).
// MEF-ADR-0024 decision #8: se consume directo con IPrivateEventHandlerAsync, sin comando espejo.
// MEF-ADR-0009: partial class para soportar clase Mensajes en archivo separado.
public partial class RegistroDeMarcacionCreadoEventHandler
    : IPrivateEventHandlerAsync<RegistroDeMarcacionCreado>
{
    private readonly ILectorSedesParaMarcacion _lector;
    private readonly IPrivateEventSender _privateEventSender;
    private readonly ILogger<RegistroDeMarcacionCreadoEventHandler> _logger;

    public RegistroDeMarcacionCreadoEventHandler(
        ILectorSedesParaMarcacion lector,
        IPrivateEventSender privateEventSender,
        ILogger<RegistroDeMarcacionCreadoEventHandler> logger)
    {
        _lector = lector;
        _privateEventSender = privateEventSender;
        _logger = logger;
    }

    public async Task HandleAsync(RegistroDeMarcacionCreado @event, CancellationToken ct = default)
    {
        if (@event.DispositivoId is null)
            return;

        var ubicacion = await _lector.BuscarUbicacionAsync(@event.DispositivoId, ct);
        if (ubicacion is null)
        {
            _logger.LogWarning(
                Mensajes.DispositivoDesconocidoMarcando,
                @event.CodigoColaborador,
                @event.DispositivoId);
            return;
        }

        var fichaSede = await _lector.BuscarFichaSedeAsync(ubicacion.SedeId, ct);
        if (fichaSede is null)
        {
            _logger.LogWarning(
                Mensajes.DispositivoDesconocidoMarcando,
                @event.CodigoColaborador,
                @event.DispositivoId);
            return;
        }

        var evento = new SedeDeMarcacionResuelta(
            @event.CodigoColaborador,
            @event.TimestampNormalizado,
            @event.DispositivoId,
            fichaSede.Codigo,
            fichaSede.Nombre,
            fichaSede.CentroDeCostos);

        await _privateEventSender.PublishAsync(
            new PublishOptions { GroupId = @event.CodigoColaborador },
            evento);
    }
}
