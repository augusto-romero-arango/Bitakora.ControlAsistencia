namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

// Issue #183: payload plano (100% primitivo) del dia, lo que nomina consume realmente.
// Reemplaza el modelo rico que viajaba en DiaCalculado (DesgloseHoras + DetalleControlFranja),
// el cual dependia del resolver custom de Marten y se serializaba lossy en el canal de
// publicacion a Service Bus (field notes 2026-06-23). Con solo primitivos, ningun consumidor
// depende de nuestra serializacion interna y ese bug se vuelve estructuralmente imposible.
//
// ADR-0002: vive en Contracts (contrato cross-domain; consumidor: sistema de nomina).
// Serializacion: record con primitivos; STJ lo serializa/deserializa nativo SIN ConfigurarSerializacion.
//
// MinutosPorConcepto: clave = Concepto.ToString() ("OrdinariaDiurna", ...) o la clave literal
//   "Retardo"; valor = minutos agregados del dia para esa clave.
// Trazabilidad: textos de auditoria del calculo. En #183 viaja vacia; su generacion es un issue aparte.
//
// Igualdad por valor de las colecciones via override manual de Equals/GetHashCode (precedente
// DetalleFranjaOrdinaria, #129): el record por defecto compara MinutosPorConcepto/Trazabilidad por
// referencia. El override lo implementa la fase verde (este es el stub minimo).
public record HorasDiscriminadas(
    IReadOnlyDictionary<string, int> MinutosPorConcepto,
    IReadOnlyList<string> Trazabilidad);
