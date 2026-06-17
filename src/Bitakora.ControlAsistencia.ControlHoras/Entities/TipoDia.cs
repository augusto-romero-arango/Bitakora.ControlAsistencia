// Issue #134: Clasificar segmentos horarios por banda y tipo de dia
namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

/// <summary>
/// Tipo de dia segun el marco legal colombiano para recargos.
/// CA-9: un dia que es simultaneamente domingo y festivo se clasifica como
/// DominicalFestivo una sola vez - no se duplica el recargo.
/// </summary>
public enum TipoDia
{
    /// <summary>Lunes a sabado no festivo.</summary>
    Habil,

    /// <summary>Domingo, festivo, o ambos simultaneamente.</summary>
    DominicalFestivo
}
