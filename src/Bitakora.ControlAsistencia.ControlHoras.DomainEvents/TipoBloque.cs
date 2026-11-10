namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Tipo de bloque resultante de <see cref="TurnoDiario.Segmentar"/>. Descansos y extras son
/// sub-franjas contenidas en la franja ordinaria; la segmentacion las trata igual: ambas recortan
/// la ordinaria en bloques tipados.
/// </summary>
public enum TipoBloque
{
    /// <summary>Tramo de trabajo ordinario, fuera de descansos y extras.</summary>
    Ordinaria,

    Descanso,

    Extra
}
