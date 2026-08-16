namespace Bitakora.ControlAsistencia.ReadModels.ControlHoras;

/// <summary>
/// Tipo de bloque resultante de segmentar el turno vigente (issue #328, misma semantica que
/// <c>TipoBloque</c> de ControlHoras.DomainEvents, issue #327). ReadModels es isla (cero
/// referencias de proyecto, no conoce DomainEvents ni buses): este enum se duplica con paridad de
/// nombre y valores en vez de importarlo, mismo criterio de "tres islas" que ya aplican
/// ColaboradorProgramado, TurnoDiario, FranjaProgramada y SubFranjaProgramada entre
/// PublicEvents/PrivateEvents/DomainEvents.
/// La clase de proyeccion companion (TurnoVigenteProjection, en el worker) es quien mapea el
/// TipoBloque de DomainEvents a este.
/// </summary>
public enum TipoBloque
{
    /// <summary>Tramo de trabajo ordinario, fuera de descansos y extras.</summary>
    Ordinaria,

    /// <summary>Tramo de descanso contenido dentro de la franja ordinaria.</summary>
    Descanso,

    /// <summary>Tramo de trabajo extra contenido dentro de la franja ordinaria.</summary>
    Extra
}
