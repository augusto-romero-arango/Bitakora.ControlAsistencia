namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Issue #425: ciclo de vida de DiaCalculado. Issue #489: Aprobado cierra la transicion
// Provisional -> Aprobado -- el dia queda en firme y sus valores ya no cambian automaticamente
// ante nuevas fotos (glosario: "Aprobado"). Conciliar y reabrir quedan para issues futuros.
public enum EstadoDiaCalculado
{
    Provisional,
    Aprobado
}
