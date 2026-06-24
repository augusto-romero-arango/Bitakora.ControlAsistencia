namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

// Issue #183: payload primitivo del desglose del dia que viaja en DiaCalculado hacia nomina.
// Cura de raiz del bug del smoke CA-5 (NullReferenceException, field notes 2026-06-23-1924):
// el contrato NO contiene ningun tipo de dominio rico (IntervaloTemporal, DetalleRetardo), solo
// primitivos. Asi ningun consumidor depende de la serializacion interna de ControlHoras y el
// payload sobrevive el roundtrip con el serializador por defecto del publisher (sin resolver custom).
//
// ADR-0002: vive en Contracts por ser contrato cross-domain.
// ADR-0015: record con constructor primario sobre primitivos - STJ lo serializa nativo, sin
// ConfigurarSerializacion. No sobreescribe Equals/GetHashCode: igual que los hermanos DesgloseHoras
// y DesgloseFranja, se compara estructuralmente (el harness usa BeEquivalentTo); ningun consumidor
// de este repo lo compara por valor.
//
// MinutosPorConcepto: clave = Concepto.ToString() ("OrdinariaDiurna", ...) o la clave literal
//                     "Retardo"; valor = minutos agregados del dia para esa clave.
// Trazabilidad: lista textual de auditoria. En el issue #183 queda VACIA; su generacion es el
//               issue "Generar trazabilidad".
public record HorasDiscriminadas(
    IReadOnlyDictionary<string, int> MinutosPorConcepto,
    IReadOnlyList<string> Trazabilidad);
