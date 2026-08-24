namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Issue #425: ciclo de vida de DiaCalculado. Unico valor de este issue -- Aprobado (y las
// transiciones Provisional -> Aprobado, conciliar, reabrir, avalar dias vacios) llega con el
// issue de acciones del Aprobador. El estado es ciclo de vida del aggregate, no campo informativo.
public enum EstadoDiaCalculado
{
    Provisional
}
