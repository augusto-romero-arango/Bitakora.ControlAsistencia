namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

// Extraido de SolicitarProgramacionTurnoTool (MEF-ADR-0018: segunda aparicion, con
// CrearTurno/AgregarFranja/etc. ya planeadas en #609-#611). Encapsula GET programacion/turnos +
// normalizacion + busqueda por nombre; cada tool consumidora sigue formateando su propio mensaje
// de "no encontrado" con su .resx (MEF-ADR-0009) a partir de NombresDisponibles.
public class ResolutorTurnoPorNombre(ProgramacionApi programacion)
{
    public const int MaximoTurnosEnMensaje = 20;

    public Task<ResultadoResolucionTurno> ResolverAsync(string nombre, CancellationToken ct) =>
        throw new NotImplementedException();
}

/// <summary>
/// Resultado de resolver un turno por nombre: exactamente uno de Ficha/FalloDeLectura viene
/// poblado cuando el turno se encuentra o la lectura del catalogo falla; NombresDisponibles
/// alimenta el mensaje TurnoNoExiste de la tool consumidora cuando ninguno de los dos aplica.
/// </summary>
public sealed record ResultadoResolucionTurno(
    FichaTurno? Ficha,
    string? FalloDeLectura,
    IReadOnlyList<string> NombresDisponibles);
