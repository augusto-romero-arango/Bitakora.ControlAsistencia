namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

/// <summary>
/// Tipo de dia segun el marco legal colombiano para recargos. Un dia que es domingo y festivo a
/// la vez se clasifica como DominicalFestivo una sola vez: no se duplica el recargo.
/// </summary>
public enum TipoDia
{
    /// <summary>Lunes a sabado no festivo.</summary>
    Habil,

    /// <summary>Domingo, festivo, o ambos simultaneamente.</summary>
    DominicalFestivo
}
