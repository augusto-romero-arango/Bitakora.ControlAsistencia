using System.Text.RegularExpressions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using ComandoCrearPlantillaSemanal =
    Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction.CrearPlantillaSemanal;

namespace Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction.CommandHandler;

public partial class CrearPlantillaSemanalCommandHandler : ICommandHandlerAsync<ComandoCrearPlantillaSemanal>
{
    private readonly IEventStore _eventStore;
    private readonly ILectorNombresPlantillaSemanal _lectorNombres;

    public CrearPlantillaSemanalCommandHandler(
        IEventStore eventStore, ILectorNombresPlantillaSemanal lectorNombres)
    {
        _eventStore = eventStore;
        _lectorNombres = lectorNombres;
    }

    public async Task HandleAsync(ComandoCrearPlantillaSemanal command, CancellationToken ct = default)
    {
        var existe = await _eventStore.ExistsAsync<PlantillaSemanalTurnos>(command.PlantillaId, ct);
        if (existe)
            throw new RecursoYaExisteException(Mensajes.PlantillaYaExiste);

        var nombresVigentes = await _lectorNombres.ObtenerNombresAsync(ct);
        var nombreNormalizado = NormalizarNombre(command.Nombre);
        var duplicado = nombresVigentes.Any(nombre =>
            string.Equals(NormalizarNombre(nombre), nombreNormalizado, StringComparison.Ordinal));
        if (duplicado)
            throw new ReglaDeNegocioDeclinadaException(Mensajes.NombreDuplicado);

        var evento = PlantillaSemanalCreada.Crear(command.PlantillaId, command.Nombre, command.Semanas);
        var plantilla = PlantillaSemanalTurnos.Iniciar(evento);
        _eventStore.StartStream(plantilla);
    }

    // Trim de extremos + colapso de espacios internos + case-folding. Los acentos SON
    // significativos (decision del experto, issue #497): ToUpperInvariant no los remueve, y por eso
    // la comparacion final es Ordinal sobre los dos nombres ya normalizados. Copia deliberada de
    // CrearTurnoCommandHandler: segunda ocurrencia, MEF-ADR-0018 mantiene la duplicacion hasta la
    // tercera -- si cambia esta regla, cambia alla tambien.
    private static string NormalizarNombre(string nombre) =>
        EspaciosConsecutivos().Replace(nombre.Trim(), " ").ToUpperInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosConsecutivos();
}
