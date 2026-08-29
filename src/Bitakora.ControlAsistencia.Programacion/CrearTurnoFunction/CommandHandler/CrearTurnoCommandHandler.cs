using System.Text.RegularExpressions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using ComandoCrearTurno = Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CrearTurno;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CommandHandler;

// La excepcion ES el canal de respuesta: InvalidOperationException -> 409 Conflict, y la
// AggregateException del factory se deja propagar -> 400 Bad Request (MEF-ADR-0004/CA-ADR-0030,
// comando HTTP sin consumidores downstream). No envolver en try/catch ni degradar a resultado.
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

    // Trim de extremos + colapso de espacios internos + case-folding. Los acentos SON
    // significativos (decision del experto, issue #497): ToUpperInvariant no los remueve, y por eso
    // la comparacion final es Ordinal sobre los dos nombres ya normalizados.
    private static string NormalizarNombre(string nombre) =>
        EspaciosConsecutivos().Replace(nombre.Trim(), " ").ToUpperInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosConsecutivos();
}
