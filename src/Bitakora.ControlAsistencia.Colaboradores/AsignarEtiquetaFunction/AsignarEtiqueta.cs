namespace Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction;

// Issue #355: comando para asignar (o sobrescribir) una etiqueta dinamica -- par categoria:valor
// libre, sin catalogo previo -- a la vinculacion vigente de un colaborador. Septimo comando del
// ciclo de vida de ColaboradorAggregateRoot (desglose #348-#357).
// Issue #376 (MEF-ADR-0043 paso 2, PUT -- reemplazo completo del VO atomico Etiqueta): Trigger HTTP
// PUT, Route: colaboradores/{id}/etiquetas/{categoria}. TipoIdentificacion/NumeroIdentificacion ya
// no llegan en el body -- el endpoint los deriva de {id} via Identificacion.Parsear (MEF-ADR-0037
// seccion 2); Categoria viene de la ruta; el body se reduce a { "valor": "..." }
// (AsignarEtiquetaBody). Este record SIGUE siendo el comando interno tal cual (forma sin cambios,
// decision del issue): el endpoint lo compone desde ruta + body antes de despacharlo.
// Payload primitivo -- igual que los demas comandos del ciclo de vida (MEF-ADR-0039 decision 6,
// payload por rol): NUNCA reusa un tipo de Colaboradores.DomainEvents como campo. El handler
// construye Etiqueta a partir de Categoria/Valor (parseo tipado unico en el borde, MEF-ADR-0037
// seccion 2, mismo criterio que Identificacion).
public record AsignarEtiqueta(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Categoria,
    string Valor);
