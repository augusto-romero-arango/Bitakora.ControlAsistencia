using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.PrivateEvents.Sedes;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;

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
        // Marcacion sin dispositivo: dato ausente legitimo, no anomalia; sin warning.
        if (@event.DispositivoId is null)
            return;

        var ubicacion = await _lector.BuscarUbicacionAsync(@event.DispositivoId, ct);
        var fichaSede = ubicacion is null
            ? null
            : await _lector.BuscarFichaSedeAsync(ubicacion.SedeId, ct);

        // Maestro incompleto (dispositivo sin ubicar o sede sin ficha): la marcacion queda sin sede y
        // se corrige el maestro, no se reintenta. Tambien cubre la ventana de consistencia eventual
        // de la proyeccion, indistinguible aqui.
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
