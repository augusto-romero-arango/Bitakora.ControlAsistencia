namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

// Issue #114: Enum de conceptos legales de horas segun legislacion laboral colombiana.
public enum Concepto
{
    OrdinariaDiurna,
    OrdinariaNocturna,
    ExtraDiurna,
    ExtraNocturna,
    DominicalFestivaDiurna,
    DominicalFestivaNocturna,
    ExtraDiurnaDominicalFestiva,
    ExtraNocturnaDominicalFestiva,
    Descanso
}
