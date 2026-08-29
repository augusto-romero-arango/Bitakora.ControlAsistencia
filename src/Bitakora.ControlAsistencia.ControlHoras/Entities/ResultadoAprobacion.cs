namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Resultado de DiaCalculadoAggregateRoot.Aprobar. Mecanismo "declinar con resultado" (CA-ADR-0030):
// el aggregate deriva la validez de la aprobacion desde su propio estado y candidatas (Tell-don't-Ask,
// MEF-ADR-0012) y el handler traduce cada valor de fallo a InvalidOperationException (-> 409).
// internal: mismo criterio de visibilidad que ResultadoEstampadoSede -- vive en el mismo ensamblado
// que el handler que lo consume.
internal enum ResultadoAprobacion
{
    Aprobado,
    ConflictosSinDecidir,
    CodigoSedeNoCandidata,
    DecisionParaFranjaInvalida,
    DiaYaAprobado
}
