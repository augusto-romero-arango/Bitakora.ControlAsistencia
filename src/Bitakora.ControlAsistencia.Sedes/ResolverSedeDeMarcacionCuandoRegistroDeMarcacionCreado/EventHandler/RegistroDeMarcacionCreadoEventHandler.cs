using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.PrivateEvents.Sedes;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;

// Reaccion del dueno del dato (MEF-ADR-0046 paso 2): resuelve la sede de una marcacion contra el
// read-side propio de Sedes. Consume el mismo evento que ControlHoras, cada uno con su propia
// subscription (MEF-ADR-0001). Se despacha directo como IPrivateEventHandlerAsync, sin comando
// espejo (MEF-ADR-0024 decision #8). partial: la clase Mensajes vive en archivo aparte
// (MEF-ADR-0009).
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
        // Dato ausente legitimo (marcacion sin dispositivo), no anomalia: silencio total.
        if (@event.DispositivoId is null)
            return;

        var ubicacion = await _lector.BuscarUbicacionAsync(@event.DispositivoId, ct);
        var fichaSede = ubicacion is null
            ? null
            : await _lector.BuscarFichaSedeAsync(ubicacion.SedeId, ct);

        // Maestro incompleto -- dispositivo sin ubicar, o ubicado en una sede sin ficha: la
        // marcacion queda sin sede (como hoy) y la correccion es arreglar el maestro, no reintentar.
        // Cubre tambien la ventana de consistencia eventual de la proyeccion, indistinguible aqui.
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
