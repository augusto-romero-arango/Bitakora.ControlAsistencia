namespace Bitakora.ControlAsistencia.ReadModels.Programacion;

/// <summary>
/// Limites de una Jornada para que el Programador elija cual asignar. Un documento por Jornada;
/// Id es el stream key (JornadaId.ToString()). Tiempos siempre en minutos.
/// </summary>
public sealed record LimitesDeJornada(
    string Id,
    int HorasSemanalesEnMinutos,
    int TopeDiarioEnMinutos,
    int MinimoDiarioEnMinutos,
    int DiasDescansoPorSemana,
    string Descripcion);
