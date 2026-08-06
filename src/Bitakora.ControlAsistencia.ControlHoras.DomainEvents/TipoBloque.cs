namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Tipo de bloque resultante de segmentar un turno diario (<see cref="TurnoDiario.Segmentar"/>,
/// issue #327). El glosario define los descansos y las extras como sub-franjas contenidas en la
/// franja ordinaria; el algoritmo de segmentacion las trata igual: ambas recortan la ordinaria en
/// bloques tipados.
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
