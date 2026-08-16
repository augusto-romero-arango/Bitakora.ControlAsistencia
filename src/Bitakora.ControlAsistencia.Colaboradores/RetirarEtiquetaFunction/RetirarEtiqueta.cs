namespace Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction;

// Issue #355: comando para retirar una etiqueta dinamica de la vinculacion vigente de un
// colaborador. Octavo comando del ciclo de vida de ColaboradorAggregateRoot (desglose #348-#357),
// gemelo de AsignarEtiqueta sobre el mismo diccionario.
// Issue #376 (MEF-ADR-0043 paso 3, DELETE -- remocion veraz y sin payload de un sub-recurso
// direccionable): Trigger HTTP DELETE, Route: colaboradores/{id}/etiquetas/{categoria}, SIN body.
// TipoIdentificacion/NumeroIdentificacion ya no llegan en un body -- el endpoint los deriva de {id}
// via Identificacion.Parsear (MEF-ADR-0037 seccion 2); Categoria viene de la ruta. Este record
// SIGUE siendo el comando interno tal cual (forma sin cambios, decision del issue): el endpoint lo
// compone integramente desde la ruta antes de despacharlo -- no hay IRequestValidator involucrado
// (RetirarEtiquetaValidator, que validaba el body viejo, se elimina: sin body no hay nada que
// deserializar ni validar en ese punto).
// Payload primitivo -- SIN Valor (retirar solo necesita la categoria, MEF-ADR-0039 decision 6): el
// handler obtiene la forma normalizada via Etiqueta.NormalizarCategoria (#355, ver Etiqueta.cs).
public record RetirarEtiqueta(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Categoria);
