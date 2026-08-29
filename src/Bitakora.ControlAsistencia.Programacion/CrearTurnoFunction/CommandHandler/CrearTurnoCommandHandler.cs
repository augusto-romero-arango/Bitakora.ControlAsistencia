using System.Text.RegularExpressions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using ComandoCrearTurno = Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CrearTurno;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CommandHandler;

// HU-4: Handler que crea un nuevo turno de trabajo
// Flujo: verificar idempotencia -> construir evento via TurnoCreado.Crear(comando)
//        -> iniciar stream del aggregate -> persistir
// ADR-0007: lanza InvalidOperationException si el turno ya existe (-> 409 Conflict)
//           deja propagar AggregateException del factory (-> 400 Bad Request)
public partial class CrearTurnoCommandHandler : ICommandHandlerAsync<ComandoCrearTurno>
{
    private readonly IEventStore _eventStore;
    private readonly ILectorNombresTurno _lectorNombres;

    public CrearTurnoCommandHandler(IEventStore eventStore, ILectorNombresTurno lectorNombres)
    {
        _eventStore = eventStore;
        _lectorNombres = lectorNombres;
    }

    public async Task HandleAsync(ComandoCrearTurno command, CancellationToken ct = default)
    {
        var existe = await _eventStore.ExistsAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (existe)
            throw new InvalidOperationException(Mensajes.TurnoYaExiste);

        var nombresVigentes = await _lectorNombres.ObtenerNombresAsync(ct);
        var nombreNormalizado = NormalizarNombre(command.Nombre);
        var duplicado = nombresVigentes.Any(nombre =>
            string.Equals(NormalizarNombre(nombre), nombreNormalizado, StringComparison.Ordinal));
        if (duplicado)
            throw new InvalidOperationException(Mensajes.NombreDuplicado);

        var evento = command.EsDescanso
            ? TurnoCreado.CrearDescanso(command.TurnoId, command.Nombre)
            : TurnoCreado.Crear(command.TurnoId, command.Nombre, command.ToDatosFranjas());
        var catalogo = CatalogoTurnos.Iniciar(evento);
        _eventStore.StartStream(catalogo);
    }

    // Trim de extremos + colapso de espacios internos + comparacion case-insensitive. Los acentos
    // SON significativos (decision del experto, issue #497): ToUpperInvariant no los remueve.
    private static string NormalizarNombre(string nombre) =>
        Regex.Replace(nombre.Trim(), @"\s+", " ").ToUpperInvariant();
}
