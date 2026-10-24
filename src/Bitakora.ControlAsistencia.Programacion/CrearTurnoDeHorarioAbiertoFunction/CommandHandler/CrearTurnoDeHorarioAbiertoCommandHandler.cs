using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Comando = Bitakora.ControlAsistencia.Programacion.CrearTurnoDeHorarioAbiertoFunction.CrearTurnoDeHorarioAbierto;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoDeHorarioAbiertoFunction.CommandHandler;

public partial class CrearTurnoDeHorarioAbiertoCommandHandler : ICommandHandlerAsync<Comando>
{
    private readonly IEventStore _eventStore;
    private readonly ILectorNombresTurno _lectorNombres;

    public CrearTurnoDeHorarioAbiertoCommandHandler(IEventStore eventStore, ILectorNombresTurno lectorNombres)
    {
        _eventStore = eventStore;
        _lectorNombres = lectorNombres;
    }

    public async Task HandleAsync(Comando command, CancellationToken ct = default)
    {
        var catalogo = CatalogoTurnos.IniciarDeHorarioAbierto(
            command.TurnoId, command.Nombre, command.ToDuracion());

        var existe = await _eventStore.ExistsAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (existe)
            throw new RecursoYaExisteException(Mensajes.TurnoYaExiste);

        var nombresVigentes = await _lectorNombres.ObtenerNombresAsync(ct);
        var nombreNormalizado = NombreDeCatalogo.Normalizar(command.Nombre);
        var duplicado = nombresVigentes.Any(nombre =>
            string.Equals(NombreDeCatalogo.Normalizar(nombre), nombreNormalizado, StringComparison.Ordinal));
        if (duplicado)
            throw new ReglaDeNegocioDeclinadaException(Mensajes.NombreDuplicado);

        _eventStore.StartStream(catalogo);
    }
}
