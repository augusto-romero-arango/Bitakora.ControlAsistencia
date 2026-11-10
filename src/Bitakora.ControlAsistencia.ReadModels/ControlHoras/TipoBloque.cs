namespace Bitakora.ControlAsistencia.ReadModels.ControlHoras;

/// <summary>
/// Tipo de bloque del turno vigente. Duplica con paridad de nombre y valores el <c>TipoBloque</c> de
/// ControlHoras.DomainEvents, que ReadModels no referencia: si cambia uno, cambia el otro.
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
