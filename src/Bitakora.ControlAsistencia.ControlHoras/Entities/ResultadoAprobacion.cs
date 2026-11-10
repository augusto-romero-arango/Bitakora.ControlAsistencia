namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Resultado de DiaCalculadoAggregateRoot.Aprobar: el aggregate declina con resultado (CA-ADR-0030) y
// el handler traduce cada valor de fallo a ReglaDeNegocioDeclinadaException (-> 409).
internal enum ResultadoAprobacion
{
    Aprobado,
    ConflictosSinDecidir,
    CodigoSedeNoCandidata,
    DecisionParaFranjaInvalida,
    DiaYaAprobado,
    MarcacionesAmbiguasSinResolver
}
