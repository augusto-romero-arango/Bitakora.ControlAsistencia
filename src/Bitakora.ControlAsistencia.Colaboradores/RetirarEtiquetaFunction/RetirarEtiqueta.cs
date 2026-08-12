namespace Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction;

// Issue #355: comando para retirar una etiqueta dinamica de la vinculacion vigente de un
// colaborador. Octavo comando del ciclo de vida de ColaboradorAggregateRoot (desglose #348-#357),
// gemelo de AsignarEtiqueta sobre el mismo diccionario.
// Trigger: HTTP POST, Route: Colaboradores/Etiquetas/Retiros (sub-recurso de las etiquetas, patron
// de AnularTerminacion/TerminarVinculacion #349/#354; identificacion en el body porque su
// representacion "CC:79543210" contiene ":", hostil como segmento de URL). DELETE descartado --
// identidad en el body, consistencia POST del BC (decision del planner).
// Payload primitivo -- SIN Valor (retirar solo necesita la categoria, MEF-ADR-0039 decision 6): el
// handler obtiene la forma normalizada via Etiqueta.NormalizarCategoria (#355, ver Etiqueta.cs).
public record RetirarEtiqueta(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Categoria);
